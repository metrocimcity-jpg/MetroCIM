using Autodesk.Revit.DB;
using RevitXKT.Models;
using RevitColor = Autodesk.Revit.DB.Color;

namespace RevitXKT.Services;

public sealed class ColorResolver
{
    private static readonly BuiltInParameter[] SystemTypeParameters =
    [
        BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM,
        BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM
    ];

    public ResolvedAppearance Resolve(Element element, View3D view)
    {
        Element source = GetAppearanceSource(element);
        int? transparency = null;

        if (TryGetFilterAppearance(source, view, out ResolvedAppearance filterColor, out int? filterTransparency))
        {
            transparency = filterTransparency ?? transparency;
            return WithTransparency(filterColor, transparency);
        }

        if (TryGetOverrideAppearance(source, view, out ResolvedAppearance overrideColor, out int? overrideTransparency))
        {
            transparency = overrideTransparency ?? transparency;
            return WithTransparency(overrideColor, transparency);
        }

        if (TryGetSystemTypeAppearance(source, view, out ResolvedAppearance systemColor, out int? systemTransparency))
        {
            transparency = systemTransparency ?? transparency;
            return WithTransparency(systemColor, transparency);
        }

        return WithTransparency(GetCategoryDefaultAppearance(source), transparency);
    }

    private static Element GetAppearanceSource(Element element)
    {
        if (element is InsulationLiningBase insulation &&
            insulation.HostElementId != ElementId.InvalidElementId &&
            element.Document.GetElement(insulation.HostElementId) is Element host)
        {
            return host;
        }

        return element;
    }

    private static bool TryGetFilterAppearance(
        Element element,
        View3D view,
        out ResolvedAppearance appearance,
        out int? transparency)
    {
        appearance = default;
        transparency = null;
        ResolvedAppearance? lastColor = null;

        IList<ElementId> filterIds;
        try
        {
            filterIds = view.GetOrderedFilters();
        }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
        {
            return false;
        }

        foreach (ElementId filterId in filterIds)
        {
            if (!view.GetIsFilterEnabled(filterId) || !view.GetFilterVisibility(filterId))
                continue;

            if (view.Document.GetElement(filterId) is not FilterElement filter)
                continue;

            if (!ElementPassesFilter(element, filter))
                continue;

            OverrideGraphicSettings overrides = view.GetFilterOverrides(filterId);
            if (TryGetSurfaceColor(overrides, out ResolvedAppearance color))
                lastColor = color;

            if (TryGetTransparency(overrides, out int t))
                transparency = t;
        }

        if (lastColor is null)
            return false;

        appearance = lastColor.Value;
        return true;
    }

    private static bool TryGetOverrideAppearance(
        Element element,
        View3D view,
        out ResolvedAppearance appearance,
        out int? transparency)
    {
        appearance = default;
        transparency = null;

        OverrideGraphicSettings elementOverrides = view.GetElementOverrides(element.Id);
        if (TryGetSurfaceColor(elementOverrides, out appearance))
        {
            if (TryGetTransparency(elementOverrides, out int t))
                transparency = t;
            return true;
        }

        if (TryGetTransparency(elementOverrides, out int elementTransparency))
            transparency = elementTransparency;

        if (element.Category is not null)
        {
            try
            {
                OverrideGraphicSettings categoryOverrides = view.GetCategoryOverrides(element.Category.Id);
                if (TryGetSurfaceColor(categoryOverrides, out appearance))
                {
                    if (TryGetTransparency(categoryOverrides, out int t))
                        transparency ??= t;
                    return true;
                }

                if (TryGetTransparency(categoryOverrides, out int categoryTransparency))
                    transparency ??= categoryTransparency;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // Category does not support graphic overrides.
            }
        }

        return false;
    }

    private static bool TryGetSystemTypeAppearance(
        Element element,
        View3D view,
        out ResolvedAppearance appearance,
        out int? transparency)
    {
        appearance = default;
        transparency = null;

        if (element.Category is not null)
        {
            try
            {
                ElementId schemeId = view.GetColorFillSchemeId(element.Category.Id);
                if (schemeId != ElementId.InvalidElementId &&
                    view.Document.GetElement(schemeId) is ColorFillScheme scheme &&
                    TryMatchColorFill(element, scheme, out appearance))
                {
                    return true;
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // Category does not support color fill in this view.
            }
        }

        return TryGetMepSystemColor(element, out appearance);
    }

    private static ResolvedAppearance GetCategoryDefaultAppearance(Element element)
    {
        Document doc = element.Document;

        ICollection<ElementId> materialIds = [];
        try
        {
            materialIds = element.GetMaterialIds(false);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            // Element does not support material quantities.
        }

        foreach (ElementId materialId in materialIds)
        {
            if (doc.GetElement(materialId) is Material material &&
                TryFromRevitColor(material.Color, material.Transparency, out ResolvedAppearance fromMaterial) &&
                !IsNearBlack(fromMaterial))
            {
                return fromMaterial;
            }
        }

        if (element.Category?.Material is { } categoryMaterial &&
            TryFromRevitColor(categoryMaterial.Color, categoryMaterial.Transparency, out ResolvedAppearance fromCategoryMaterial) &&
            !IsNearBlack(fromCategoryMaterial))
        {
            return fromCategoryMaterial;
        }

        return ResolvedAppearance.Default;
    }

    private static bool ElementPassesFilter(Element element, FilterElement filter)
    {
        try
        {
            switch (filter)
            {
                case ParameterFilterElement parameterFilter:
                    ICollection<ElementId> categories = parameterFilter.GetCategories();
                    if (element.Category is null || !categories.Contains(element.Category.Id))
                        return false;

                    ElementFilter? rules = parameterFilter.GetElementFilter();
                    return rules is null || rules.PassesFilter(element);

                case SelectionFilterElement selectionFilter:
                    return selectionFilter.GetElementIds().Contains(element.Id);

                default:
                    return false;
            }
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    private static bool TryMatchColorFill(Element element, ColorFillScheme scheme, out ResolvedAppearance appearance)
    {
        appearance = default;
        Parameter? parameter = FindParameter(element, scheme.ParameterDefinition);
        if (parameter is null || !parameter.HasValue)
            return false;

        foreach (ColorFillSchemeEntry entry in scheme.GetEntries())
        {
            if (!entry.IsVisible)
                continue;

            if (!EntryMatches(parameter, entry))
                continue;

            if (TryFromRevitColor(entry.Color, 0, out appearance))
                return true;
        }

        return false;
    }

    private static bool EntryMatches(Parameter parameter, ColorFillSchemeEntry entry)
    {
        if (parameter.StorageType != entry.StorageType)
            return false;

        return entry.StorageType switch
        {
            StorageType.Integer => parameter.AsInteger() == entry.GetIntegerValue(),
            StorageType.Double => Math.Abs(parameter.AsDouble() - entry.GetDoubleValue()) < 1e-6,
            StorageType.String => string.Equals(parameter.AsString() ?? string.Empty, entry.GetStringValue() ?? string.Empty, StringComparison.Ordinal),
            StorageType.ElementId => parameter.AsElementId() == entry.GetElementIdValue(),
            _ => false
        };
    }

    private static bool TryGetMepSystemColor(Element element, out ResolvedAppearance appearance)
    {
        appearance = default;
        Document doc = element.Document;

        if (TryGetSystemTypeId(element, out ElementId systemTypeId) &&
            doc.GetElement(systemTypeId) is MEPSystemType fromParameter &&
            TryFromSystemType(fromParameter, out appearance))
        {
            return true;
        }

        if (element is MEPCurve curve)
        {
            try
            {
                MEPSystem? system = curve.MEPSystem;
                if (system is not null &&
                    doc.GetElement(system.GetTypeId()) is MEPSystemType systemType &&
                    TryFromSystemType(systemType, out appearance))
                {
                    return true;
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // Unassigned or invalid MEP system.
            }
        }

        if (element is FamilyInstance instance)
        {
            try
            {
                ConnectorManager? manager = instance.MEPModel?.ConnectorManager;
                if (manager is not null)
                {
                    foreach (Connector connector in manager.Connectors.Cast<Connector>())
                    {
                        if (connector.MEPSystem is { } system &&
                            doc.GetElement(system.GetTypeId()) is MEPSystemType systemType &&
                            TryFromSystemType(systemType, out appearance))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // Family has no MEP connectors.
            }
        }

        return false;
    }

    private static bool TryGetSystemTypeId(Element element, out ElementId systemTypeId)
    {
        foreach (BuiltInParameter builtIn in SystemTypeParameters)
        {
            try
            {
                Parameter? parameter = element.get_Parameter(builtIn);
                if (parameter is { HasValue: true, StorageType: StorageType.ElementId })
                {
                    ElementId id = parameter.AsElementId();
                    if (id != ElementId.InvalidElementId)
                    {
                        systemTypeId = id;
                        return true;
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // Parameter is not present on this element.
            }
        }

        systemTypeId = ElementId.InvalidElementId;
        return false;
    }

    private static bool TryFromSystemType(MEPSystemType systemType, out ResolvedAppearance appearance)
    {
        bool hasFill = TryFromRevitColor(systemType.FillColor, 0, out ResolvedAppearance fill);
        bool hasLine = TryFromRevitColor(systemType.LineColor, 0, out ResolvedAppearance line);

        if (hasFill && !IsNearBlack(fill))
        {
            appearance = fill;
            return true;
        }

        if (hasLine)
        {
            appearance = line;
            return true;
        }

        appearance = fill;
        return hasFill;
    }

    private static Parameter? FindParameter(Element element, ElementId definitionId)
    {
        if (definitionId == ElementId.InvalidElementId)
            return null;

        foreach (Parameter parameter in element.Parameters)
        {
            if (parameter.Id == definitionId)
                return parameter;
        }

        ElementId typeId = element.GetTypeId();
        if (typeId != ElementId.InvalidElementId && element.Document.GetElement(typeId) is Element type)
        {
            foreach (Parameter parameter in type.Parameters)
            {
                if (parameter.Id == definitionId)
                    return parameter;
            }
        }

        return null;
    }

    private static bool TryGetSurfaceColor(OverrideGraphicSettings settings, out ResolvedAppearance appearance)
    {
        if (TryFromRevitColor(settings.SurfaceForegroundPatternColor, settings.Transparency, out appearance))
            return true;

        if (TryFromRevitColor(settings.CutForegroundPatternColor, settings.Transparency, out appearance))
            return true;

        appearance = default;
        return false;
    }

    private static bool TryGetTransparency(OverrideGraphicSettings settings, out int transparency)
    {
        transparency = settings.Transparency;
        return transparency > 0;
    }

    private static bool TryFromRevitColor(RevitColor? color, int transparencyPercent, out ResolvedAppearance appearance)
    {
        appearance = default;
        if (color is null || !color.IsValid)
            return false;

        appearance = ResolvedAppearance.FromRgb(color.Red, color.Green, color.Blue, transparencyPercent);
        return true;
    }

    private static bool IsNearBlack(ResolvedAppearance color) =>
        color.R < 16 && color.G < 16 && color.B < 16;

    private static ResolvedAppearance WithTransparency(ResolvedAppearance color, int? transparency)
    {
        if (transparency is null)
            return color;

        return ResolvedAppearance.FromRgb(color.R, color.G, color.B, transparency.Value);
    }
}
