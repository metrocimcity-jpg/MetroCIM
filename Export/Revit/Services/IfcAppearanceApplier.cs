using Autodesk.Revit.DB;
using MetroCIM.Models;
using RevitColor = Autodesk.Revit.DB.Color;

namespace MetroCIM.Services;

/// <summary>
/// Pushes IFC colors onto elements for the duration of an export.
/// Priority: view filter on this element, then graphic override, then system type, then this element's material.
/// </summary>
public sealed class IfcAppearanceApplier
{
    public static string MaterialName(ResolvedAppearance appearance) =>
        $"MetroCIM {appearance.R:D3}-{appearance.G:D3}-{appearance.B:D3}-{appearance.A:D3}";

    public static bool IsTemporaryMaterialName(string? name) =>
        name is not null && name.StartsWith("MetroCIM ", StringComparison.Ordinal);

    public static Dictionary<string, ResolvedAppearance> Apply(Document document, View view)
    {
        var colorsByIfcGuid = new Dictionary<string, ResolvedAppearance>(StringComparer.Ordinal);
        if (view is not View3D view3D || view3D.IsTemplate)
            return colorsByIfcGuid;

        var visibility = new VisibilityService();
        var colors = new ColorResolver(ColorResolveMode.Ifc);
        colors.Bind(view3D);

        var materials = new Dictionary<(byte R, byte G, byte B, byte A), ElementId>();
        var materialsByName = IndexMaterials(document);
        ElementId? solidFillId = FindSolidFill(document);

        var resolved = new List<(Element Element, ResolvedAppearance Appearance)>();
        foreach (Element element in visibility.GetVisibleElements(document, view3D))
        {
            ResolvedAppearance appearance = colors.Resolve(element, view3D);
            resolved.Add((element, appearance));
            IndexGuids(colorsByIfcGuid, element, appearance);
        }

        bool changed = false;
        foreach ((Element element, ResolvedAppearance appearance) in resolved)
        {
            ElementId materialId = GetOrCreateMaterial(document, materials, materialsByName, appearance);
            changed |= TrySetMaterialParameter(element, materialId);
            changed |= TrySetIfcMaterialOverride(element, MaterialName(appearance));
            changed |= TrySetOverrides(view3D, element, appearance, solidFillId);
            changed |= PaintFaces(document, element, materialId);
        }

        if (changed)
            document.Regenerate();

        return colorsByIfcGuid;
    }

    public static void HarvestStoredGuids(
        Document document,
        View view,
        Dictionary<string, ResolvedAppearance> colorsByIfcGuid)
    {
        if (view is not View3D view3D || view3D.IsTemplate)
            return;

        var visibility = new VisibilityService();
        var colors = new ColorResolver(ColorResolveMode.Ifc);
        colors.Bind(view3D);
        foreach (Element element in visibility.GetVisibleElements(document, view3D))
            IndexGuids(colorsByIfcGuid, element, colors.Resolve(element, view3D));
    }

    private static Dictionary<string, ElementId> IndexMaterials(Document document)
    {
        var byName = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
        foreach (Material existing in new FilteredElementCollector(document)
                     .OfClass(typeof(Material))
                     .Cast<Material>())
        {
            byName.TryAdd(existing.Name, existing.Id);
        }

        return byName;
    }

    private static ElementId GetOrCreateMaterial(
        Document document,
        Dictionary<(byte R, byte G, byte B, byte A), ElementId> cache,
        Dictionary<string, ElementId> byName,
        ResolvedAppearance appearance)
    {
        var key = (appearance.R, appearance.G, appearance.B, appearance.A);
        if (cache.TryGetValue(key, out ElementId? cached) && cached is not null)
            return cached;

        string name = MaterialName(appearance);
        ElementId id;
        if (byName.TryGetValue(name, out ElementId? existing) && existing is not null)
        {
            id = existing;
        }
        else
        {
            id = Material.Create(document, name);
            byName[name] = id;
        }

        if (document.GetElement(id) is Material material)
            ApplyColor(material, appearance);

        cache[key] = id;
        return id;
    }

    private static void ApplyColor(Material material, ResolvedAppearance appearance)
    {
        var color = new RevitColor(appearance.R, appearance.G, appearance.B);
        int transparency = Math.Clamp((int)Math.Round((255 - appearance.A) * 100d / 255d), 0, 100);
        material.Color = color;
        material.Transparency = transparency;
        material.Shininess = 32;
        material.Smoothness = 50;
        try
        {
            material.SurfaceForegroundPatternColor = color;
            material.CutForegroundPatternColor = color;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
        }
    }

    private static bool TrySetMaterialParameter(Element element, ElementId materialId)
    {
        if (element is ElementType)
            return false;

        Parameter? parameter = element.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
        if (parameter is not { StorageType: StorageType.ElementId, IsReadOnly: false })
            return false;

        if (parameter.Element is { } owner && owner.Id != element.Id)
            return false;

        return parameter.Set(materialId);
    }

    private static bool TrySetOverrides(
        View3D view,
        Element element,
        ResolvedAppearance appearance,
        ElementId? solidFillId)
    {
        try
        {
            var color = new RevitColor(appearance.R, appearance.G, appearance.B);
            OverrideGraphicSettings overrides = view.GetElementOverrides(element.Id);
            overrides.SetSurfaceForegroundPatternColor(color);
            overrides.SetCutForegroundPatternColor(color);
            if (solidFillId is not null)
            {
                overrides.SetSurfaceForegroundPatternId(solidFillId);
                overrides.SetCutForegroundPatternId(solidFillId);
            }

            int transparency = Math.Clamp((int)Math.Round((255 - appearance.A) * 100d / 255d), 0, 100);
            overrides.SetSurfaceTransparency(transparency);
            view.SetElementOverrides(element.Id, overrides);
            return true;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }
    }

    private static bool TrySetIfcMaterialOverride(Element element, string materialName)
    {
        Parameter? parameter = element.LookupParameter("IfcSingleMaterialOverride");
        if (parameter is not { StorageType: StorageType.String, IsReadOnly: false })
            return false;

        return parameter.Set(materialName);
    }

    private static void IndexGuids(
        Dictionary<string, ResolvedAppearance> colorsByIfcGuid,
        Element element,
        ResolvedAppearance appearance)
    {
        try
        {
            Guid exportId = ExportUtils.GetExportId(element.Document, element.Id);
            colorsByIfcGuid[IfcGuid.From(exportId)] = appearance;
            if (!string.IsNullOrWhiteSpace(element.UniqueId))
                colorsByIfcGuid[element.UniqueId] = appearance;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
        }

        try
        {
            Parameter? stored = element.get_Parameter(BuiltInParameter.IFC_GUID);
            if (stored is { HasValue: true, StorageType: StorageType.String } &&
                stored.AsString() is { Length: 22 } value)
            {
                colorsByIfcGuid[value] = appearance;
            }
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
        }
    }

    private static bool PaintFaces(Document document, Element element, ElementId materialId)
    {
        Options options = new()
        {
            ComputeReferences = true,
            IncludeNonVisibleObjects = false
        };

        GeometryElement? geometry;
        try
        {
            geometry = element.get_Geometry(options);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return false;
        }

        return geometry is not null && PaintGeometry(document, element.Id, geometry, materialId);
    }

    private static bool PaintGeometry(Document document, ElementId elementId, GeometryElement geometry, ElementId materialId)
    {
        bool painted = false;
        foreach (GeometryObject obj in geometry)
        {
            switch (obj)
            {
                case Solid solid:
                    painted |= PaintSolid(document, elementId, solid, materialId);
                    break;
                case GeometryElement nested:
                    painted |= PaintGeometry(document, elementId, nested, materialId);
                    break;
            }
        }

        return painted;
    }

    private static bool PaintSolid(Document document, ElementId elementId, Solid solid, ElementId materialId)
    {
        if (solid.Faces.IsEmpty)
            return false;

        bool painted = false;
        foreach (Face face in solid.Faces)
        {
            if (face.Area < 1e-6)
                continue;

            try
            {
                document.Paint(elementId, face, materialId);
                painted = true;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // Face has no reference or cannot be painted.
            }
        }

        return painted;
    }

    private static ElementId? FindSolidFill(Document document)
    {
        foreach (FillPatternElement pattern in new FilteredElementCollector(document)
                     .OfClass(typeof(FillPatternElement))
                     .Cast<FillPatternElement>())
        {
            try
            {
                if (pattern.GetFillPattern()?.IsSolidFill == true)
                    return pattern.Id;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
            }
        }

        return null;
    }
}
