using Autodesk.Navisworks.Api;
using MetroCIM.Models;
using MetroCIM.Services;

namespace MetroCIM.Navisworks.Services;

public sealed class ExcelParameterExporter
{
    private readonly VisibilityService _visibility = new();

    public ParameterTable Collect(Document document, Action<int, int, string>? progress = null)
    {
        IReadOnlyList<ModelItem> visible = _visibility.GetVisibleGeometryItems(
            document,
            scanned => progress?.Invoke(scanned, Math.Max(scanned, 1), "Collecting visible items"));

        var table = new ParameterTable();
        int index = 0;
        foreach (ModelItem item in visible)
        {
            index++;
            progress?.Invoke(index, visible.Count, "Reading parameters");
            table.Rows.Add(BuildRow(item));
        }

        return table;
    }

    public int Export(Document document, string path, Action<int, int, string>? progress = null)
    {
        ParameterTable table = Collect(document, progress);
        progress?.Invoke(table.Rows.Count, Math.Max(table.Rows.Count, 1), "Writing Excel");
        ExcelTableWriter.Write(path, table);
        return table.Rows.Count;
    }

    private static ParameterTableRow BuildRow(ModelItem item)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CollectProperties(item, properties);

        string uniqueId = TryGetProperty(properties, "UniqueId", "Unique ID", "Element UUID", "Guid", "GUID")
            ?? (item.InstanceGuid != Guid.Empty ? item.InstanceGuid.ToString("D") : string.Empty);

        string revitId = TryGetProperty(
                properties,
                "ElementId",
                "Element ID",
                "Id",
                "Revit ID",
                "RevitId",
                "Instance ID")
            ?? string.Empty;

        string ifcGuid = TryGetProperty(
                properties,
                "IfcGUID",
                "IFC GUID",
                "Ifc Guid",
                "GlobalId",
                "Global ID")
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(ifcGuid) && item.InstanceGuid != Guid.Empty)
            ifcGuid = IfcGuid.From(item.InstanceGuid);

        if (string.IsNullOrWhiteSpace(revitId) && item.InstanceGuid != Guid.Empty)
            revitId = item.InstanceGuid.ToString("N");

        var row = new ParameterTableRow
        {
            RevitId = revitId,
            IfcGuid = ifcGuid,
            UniqueId = uniqueId,
            Category = item.ClassDisplayName ?? item.ClassName ?? string.Empty,
            Family = TryGetProperty(properties, "Family", "Family Name", "FamilyName") ?? string.Empty,
            TypeName = TryGetProperty(properties, "Type", "Type Name", "TypeName", "Element Type")
                ?? item.DisplayName
                ?? string.Empty,
            Name = item.DisplayName ?? item.ClassDisplayName ?? "Item"
        };

        foreach (KeyValuePair<string, string> pair in properties)
        {
            if (IsIdentityProperty(pair.Key))
                continue;
            row.Parameters[pair.Key] = pair.Value;
        }

        return row;
    }

    private static void CollectProperties(ModelItem item, Dictionary<string, string> properties)
    {
        try
        {
            foreach (PropertyCategory category in item.PropertyCategories)
            {
                string categoryName = string.IsNullOrWhiteSpace(category.DisplayName)
                    ? "Properties"
                    : category.DisplayName;

                foreach (DataProperty property in category.Properties)
                {
                    string name = property.DisplayName ?? property.Name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string value = property.Value.ToDisplayString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    string key = categoryName + "." + name;
                    if (!properties.ContainsKey(key))
                        properties[key] = value;

                    // Also keep a short key for identity lookups.
                    if (!properties.ContainsKey(name))
                        properties[name] = value;
                }
            }
        }
        catch
        {
            // Property harvest is best-effort.
        }
    }

    private static string? TryGetProperty(Dictionary<string, string> properties, params string[] names)
    {
        foreach (string name in names)
        {
            if (properties.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value))
                return value;

            foreach (KeyValuePair<string, string> pair in properties)
            {
                if (pair.Key.EndsWith("." + name, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(pair.Value))
                {
                    return pair.Value;
                }
            }
        }

        return null;
    }

    private static bool IsIdentityProperty(string key)
    {
        string leaf = key;
        int dot = key.LastIndexOf('.');
        if (dot >= 0 && dot < key.Length - 1)
            leaf = key.Substring(dot + 1);

        return leaf.Equals("ElementId", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("Element ID", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("Id", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("Revit ID", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("RevitId", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("IfcGUID", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("IFC GUID", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("GlobalId", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("UniqueId", StringComparison.OrdinalIgnoreCase)
            || leaf.Equals("Unique ID", StringComparison.OrdinalIgnoreCase);
    }
}
