using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using MetroCIM.Models;
using RevitColor = Autodesk.Revit.DB.Color;

namespace MetroCIM.Services;

public enum ColorResolveMode
{
    ViewShaded = 0,
    Ifc = 1
}

public sealed class ColorResolver
{
    private readonly ColorResolveMode _mode;
    private readonly MepSystemTypeColor _systems = new();
    private readonly Dictionary<ElementId, ResolvedAppearance> _byElement = [];
    private IList<(FilterElement Filter, OverrideGraphicSettings Overrides)> _activeFilters = [];
    private View3D? _boundView;

    public ColorResolver() : this(ColorResolveMode.ViewShaded)
    {
    }

    public ColorResolver(ColorResolveMode mode)
    {
        _mode = mode;
    }

    public void Bind(View3D view)
    {
        _boundView = view;
        _byElement.Clear();
        _activeFilters = [];
        try
        {
            foreach (ElementId filterId in view.GetOrderedFilters())
            {
                if (!view.GetIsFilterEnabled(filterId) || !view.GetFilterVisibility(filterId))
                    continue;
                if (view.Document.GetElement(filterId) is not FilterElement filter)
                    continue;
                _activeFilters.Add((filter, view.GetFilterOverrides(filterId)));
            }
        }
        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
        {
            _activeFilters = [];
        }
    }

    public ResolvedAppearance Resolve(Element element, View3D view)
    {
        if (!ReferenceEquals(_boundView, view))
            Bind(view);

        if (_byElement.TryGetValue(element.Id, out ResolvedAppearance cached))
            return cached;

        ResolvedAppearance appearance = ResolveUncached(element, view);
        _byElement[element.Id] = appearance;
        return appearance;
    }

    private ResolvedAppearance ResolveUncached(Element element, View3D view)
    {
        Element source = GetAppearanceSource(element);
        bool piping = IsPipingElement(source);
        int? transparency = null;

        if (_mode == ColorResolveMode.Ifc)
            return ResolveIfc(element, source, view);

        if (TryGetFilterAppearance(element, out ResolvedAppearance filterColor, out int? filterTransparency))
        {
            transparency = filterTransparency ?? transparency;
            return WithTransparency(filterColor, transparency);
        }

        if (TryGetOverrideAppearance(source, view, piping, out ResolvedAppearance overrideColor, out int? overrideTransparency))
        {
            transparency = overrideTransparency ?? transparency;
            return WithTransparency(overrideColor, transparency);
        }

        if (TryGetSystemTypeAppearance(source, view, piping, out ResolvedAppearance systemColor))
            return WithTransparency(systemColor, transparency);

        if (piping)
            return WithTransparency(ResolvedAppearance.Default, transparency);

        return WithTransparency(GetCategoryDefaultAppearance(source), transparency);
    }

    private ResolvedAppearance ResolveIfc(Element element, Element source, View3D view)
    {
        ResolvedAppearance? filter = null;
        int? transparency = null;
        if (TryGetFilterAppearance(element, out ResolvedAppearance filterColor, out int? filterTransparency))
        {
            filter = filterColor;
            transparency = filterTransparency;
        }

        ResolvedAppearance? graphicOverride = null;
        if (TryGetOverrideAppearance(element, view, IsPipingElement(element), out ResolvedAppearance overrideColor, out int? overrideTransparency))
        {
            graphicOverride = overrideColor;
            transparency ??= overrideTransparency;
        }

        ResolvedAppearance? systemOrColorFill = null;
        if (TryGetSystemTypeAppearance(element, view, IsPipingElement(element), out ResolvedAppearance systemColor) ||
            (source.Id != element.Id &&
             TryGetSystemTypeAppearance(source, view, IsPipingElement(source), out systemColor)))
        {
            systemOrColorFill = systemColor;
        }

        ResolvedAppearance? elementColor = null;
        if (TryGetElementMaterialAppearance(element, out ResolvedAppearance materialColor) ||
            (source.Id != element.Id && TryGetElementMaterialAppearance(source, out materialColor)))
        {
            elementColor = materialColor;
        }

        return WithTransparency(
            IfcColorPriority.Select(filter, graphicOverride, systemOrColorFill, elementColor),
            transparency);
    }

    private static Element GetAppearanceSource(Element element)
    {
        if (element is InsulationLiningBase insulation &&
            insulation.HostElementId != ElementId.InvalidElementId &&
            element.Document.GetElement(insulation.HostElementId) is Element host)
        {
            return host;
        }

        Element current = element;
        for (int i = 0; i < 8; i++)
        {
            if (current is not FamilyInstance instance)
                break;

            Element? parent;
            try
            {
                parent = instance.SuperComponent;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                break;
            }

            if (parent is null || parent.Id == current.Id)
                break;

            if (!IsPipingElement(parent) && !IsPipingElement(current))
                break;

            current = parent;
        }

        return current;
    }

    private bool TryGetFilterAppearance(
        Element element,
        out ResolvedAppearance appearance,
        out int? transparency)
    {
        appearance = default;
        transparency = null;
        ResolvedAppearance? lastColor = null;

        foreach ((FilterElement filter, OverrideGraphicSettings overrides) in _activeFilters)
        {
            if (!ElementPassesFilter(element, filter))
                continue;

            if (TryGetFilterOverrideColor(overrides, out ResolvedAppearance color))
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
        bool piping,
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

        if (piping)
            return false;

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

    private bool TryGetSystemTypeAppearance(
        Element element,
        View3D view,
        bool piping,
        out ResolvedAppearance appearance)
    {
        appearance = default;

        if (element.Category is not null)
        {
            try
            {
                ElementId schemeId = view.GetColorFillSchemeId(element.Category.Id);
                if (schemeId != ElementId.InvalidElementId &&
                    view.Document.GetElement(schemeId) is ColorFillScheme scheme &&
                    !(piping && MepSystemTypeColor.IsPhysicalMaterialParameter(scheme.ParameterDefinition)) &&
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

        return _systems.TryGet(element, out appearance);
    }

    private static ResolvedAppearance GetCategoryDefaultAppearance(Element element)
    {
        if (TryGetElementMaterialAppearance(element, skipNearBlack: true, out ResolvedAppearance appearance))
            return appearance;

        return ResolvedAppearance.Default;
    }

    private static bool TryGetElementMaterialAppearance(Element element, out ResolvedAppearance appearance) =>
        TryGetElementMaterialAppearance(element, skipNearBlack: false, out appearance);

    private static bool TryGetElementMaterialAppearance(Element element, bool skipNearBlack, out ResolvedAppearance appearance)
    {
        appearance = default;
        Document doc = element.Document;

        if (TryMaterialFromParameter(element, BuiltInParameter.MATERIAL_ID_PARAM, skipNearBlack, out appearance))
            return true;

        ElementId typeId = element.GetTypeId();
        if (typeId != ElementId.InvalidElementId &&
            doc.GetElement(typeId) is Element type &&
            TryMaterialFromParameter(type, BuiltInParameter.MATERIAL_ID_PARAM, skipNearBlack, out appearance))
        {
            return true;
        }

        if (TryMaterialFromPipeMaterialParameter(element, skipNearBlack, out appearance))
            return true;

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
            if (TryFromMaterialId(doc, materialId, skipNearBlack, out appearance))
                return true;
        }

        if (element.Category?.Material is { } categoryMaterial &&
            TryFromRevitColor(categoryMaterial.Color, categoryMaterial.Transparency, out appearance) &&
            !(skipNearBlack && appearance.IsNearBlack))
        {
            return true;
        }

        appearance = default;
        return false;
    }

    private static bool TryMaterialFromParameter(
        Element element,
        BuiltInParameter builtIn,
        bool skipNearBlack,
        out ResolvedAppearance appearance)
    {
        appearance = default;
        try
        {
            Parameter? parameter = element.get_Parameter(builtIn);
            if (parameter is not { HasValue: true, StorageType: StorageType.ElementId })
                return false;

            return TryFromMaterialId(element.Document, parameter.AsElementId(), skipNearBlack, out appearance);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    private static bool TryMaterialFromPipeMaterialParameter(Element element, bool skipNearBlack, out ResolvedAppearance appearance)
    {
        appearance = default;
        try
        {
            Parameter? parameter = element.get_Parameter(BuiltInParameter.RBS_PIPE_MATERIAL_PARAM);
            if (parameter is not { HasValue: true, StorageType: StorageType.ElementId })
                return false;

            ElementId id = parameter.AsElementId();
            if (TryFromMaterialId(element.Document, id, skipNearBlack, out appearance))
                return true;

            if (element.Document.GetElement(id) is not Element pipeMaterial)
                return false;

            return TryMaterialFromParameter(
                pipeMaterial,
                BuiltInParameter.MATERIAL_ID_PARAM,
                skipNearBlack,
                out appearance);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    private static bool TryFromMaterialId(
        Document document,
        ElementId materialId,
        bool skipNearBlack,
        out ResolvedAppearance appearance)
    {
        appearance = default;
        if (materialId == ElementId.InvalidElementId)
            return false;

        if (document.GetElement(materialId) is not Material material)
            return false;

        if (!TryFromRevitColor(material.Color, material.Transparency, out appearance))
            return false;

        return !(skipNearBlack && appearance.IsNearBlack);
    }

    private static bool ElementPassesFilter(Element element, FilterElement filter)
    {
        try
        {
            switch (filter)
            {
                case ParameterFilterElement parameterFilter:
                    if (!CategoryIsInFilter(element, parameterFilter))
                        return false;

                    ElementFilter? rules = parameterFilter.GetElementFilter();
                    if (rules is null)
                        return true;

                    if (PassesRules(rules, element))
                        return true;

                    // Type parameters are normally evaluated on the instance. Only retry the
                    // type when this instance's category is already in the filter — never a host
                    // or a different category such as Mechanical Equipment vs Duct Insulations.
                    ElementId typeId = element.GetTypeId();
                    return typeId != ElementId.InvalidElementId &&
                           element.Document.GetElement(typeId) is Element type &&
                           CategoryIsInFilter(element, parameterFilter) &&
                           PassesRules(rules, type);

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

    private static bool CategoryIsInFilter(Element element, ParameterFilterElement parameterFilter)
    {
        if (element.Category is null)
            return false;

        ICollection<ElementId> categories = parameterFilter.GetCategories();
        return categories.Contains(element.Category.Id);
    }

    private static bool PassesRules(ElementFilter rules, Element element)
    {
        try
        {
            return rules.PassesFilter(element);
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

    private static bool IsPipingElement(Element element)
    {
        if (element is Pipe or FlexPipe or PipeInsulation)
            return true;

        BuiltInCategory category = BuiltInCategory.INVALID;
        try
        {
            if (element.Category is not null)
                category = element.Category.BuiltInCategory;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            category = BuiltInCategory.INVALID;
        }

        if (category == BuiltInCategory.INVALID && element.Category is not null)
        {
            try
            {
                category = (BuiltInCategory)element.Category.Id.Value;
            }
            catch (InvalidCastException)
            {
                return false;
            }
        }

        return category is
            BuiltInCategory.OST_PipeCurves or
            BuiltInCategory.OST_FlexPipeCurves or
            BuiltInCategory.OST_PipeInsulations or
            BuiltInCategory.OST_PipeFitting or
            BuiltInCategory.OST_PipeAccessory or
            BuiltInCategory.OST_PlaceHolderPipes;
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

    private static bool TryGetFilterOverrideColor(OverrideGraphicSettings settings, out ResolvedAppearance appearance)
    {
        if (TryGetSurfaceColor(settings, out appearance))
            return true;

        if (TryFromRevitColor(settings.SurfaceBackgroundPatternColor, settings.Transparency, out appearance))
            return true;

        if (TryFromRevitColor(settings.CutBackgroundPatternColor, settings.Transparency, out appearance))
            return true;

        appearance = default;
        return false;
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

    private static ResolvedAppearance WithTransparency(ResolvedAppearance color, int? transparency)
    {
        if (transparency is null)
            return color;

        return ResolvedAppearance.FromRgb(color.R, color.G, color.B, transparency.Value);
    }
}
