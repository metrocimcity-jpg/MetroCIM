using Autodesk.Navisworks.Api;
using MetroCIM.Models;
using MetroCIM.Services;

namespace MetroCIM.Navisworks.Services;

public sealed class DocumentExporter
{
    private readonly VisibilityService _visibility = new();
    private readonly ColorResolver _colors = new();
    private readonly GeometryBuilder _geometry = new();

    public int ExportColorBatched(
        Document document,
        string path,
        Action<int, int, string>? progress = null,
        List<ExportedItem>? exportedItems = null)
    {
        IReadOnlyList<ModelItem> visible = _visibility.GetVisibleGeometryItems(
            document,
            scanned => progress?.Invoke(scanned, Math.Max(scanned, 1), "Collecting visible items"));

        var writer = new GltfSceneAccumulator();
        int exported = 0;
        int index = 0;

        foreach (ModelItem item in visible)
        {
            index++;
            progress?.Invoke(index, visible.Count, "Reading geometry");

            TriangleMesh? mesh = _geometry.BuildMesh(document, item);
            if (mesh is null)
                continue;

            ResolvedAppearance appearance = _colors.Resolve(item);
            writer.AddBatched(appearance, mesh);
            exportedItems?.Add(CreateExportedItem(item));
            exported++;
        }

        progress?.Invoke(visible.Count, visible.Count, "Writing glTF");
        writer.Save(path);
        return exported;
    }

    public int ExportIfc(
        Document document,
        string path,
        Action<int, int, string>? progress = null)
    {
        IReadOnlyList<ModelItem> visible = _visibility.GetVisibleGeometryItems(
            document,
            scanned => progress?.Invoke(scanned, Math.Max(scanned, 1), "Collecting visible items"));

        var items = new List<ColoredMesh>();
        int index = 0;

        foreach (ModelItem item in visible)
        {
            index++;
            progress?.Invoke(index, visible.Count, "Reading geometry");

            TriangleMesh? mesh = _geometry.BuildMesh(document, item);
            if (mesh is null)
                continue;

            string id = GetItemId(item);
            items.Add(new ColoredMesh(id, _colors.Resolve(item), mesh));
        }

        progress?.Invoke(visible.Count, visible.Count, "Writing IFC");
        IfcMeshExporter.Export(path, items, document.Title);
        return items.Count;
    }

    private static ExportedItem CreateExportedItem(ModelItem item)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (PropertyCategory category in item.PropertyCategories)
            {
                foreach (DataProperty property in category.Properties)
                {
                    string key = $"{category.DisplayName}.{property.DisplayName}";
                    string value = property.Value.ToDisplayString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(value))
                        properties[key] = value;
                }
            }
        }
        catch
        {
            // Property harvest is best-effort.
        }

        return new ExportedItem(
            GetItemId(item),
            item.DisplayName ?? item.ClassDisplayName ?? "Item",
            item.ClassDisplayName ?? item.ClassName ?? "ModelItem",
            properties);
    }

    private static string GetItemId(ModelItem item)
    {
        try
        {
            if (item.InstanceGuid != Guid.Empty)
                return item.InstanceGuid.ToString("N");
        }
        catch
        {
        }

        return "item-" + item.GetHashCode().ToString("X");
    }
}
