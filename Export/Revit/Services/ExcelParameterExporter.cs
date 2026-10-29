using Autodesk.Revit.DB;
using MetroCIM.Models;

namespace MetroCIM.Services;

public sealed class ExcelParameterExporter
{
    private readonly VisibilityService _visibility = new();

    public ParameterTable Collect(Document document, View3D view, Action<int, int, string>? progress = null)
    {
        IReadOnlyList<Element> visible = _visibility.GetVisibleElements(
            document,
            view,
            scanned => progress?.Invoke(scanned, Math.Max(scanned, 1), "Collecting visible elements"));

        var table = new ParameterTable();
        int index = 0;
        foreach (Element element in visible)
        {
            index++;
            progress?.Invoke(index, visible.Count, "Reading parameters");
            table.Rows.Add(BuildRow(document, element));
        }

        return table;
    }

    public int Export(
        Document document,
        View3D view,
        string path,
        Action<int, int, string>? progress = null)
    {
        ParameterTable table = Collect(document, view, progress);
        progress?.Invoke(table.Rows.Count, Math.Max(table.Rows.Count, 1), "Writing Excel");
        ExcelTableWriter.Write(path, table);
        return table.Rows.Count;
    }

    private static ParameterTableRow BuildRow(Document document, Element element)
    {
        var row = new ParameterTableRow
        {
            RevitId = element.Id.Value.ToString(),
            IfcGuid = GetIfcGuid(document, element),
            UniqueId = element.UniqueId ?? string.Empty,
            Category = element.Category?.Name ?? string.Empty,
            Name = ElementName(element),
            Family = string.Empty,
            TypeName = string.Empty
        };

        ElementType? elementType = document.GetElement(element.GetTypeId()) as ElementType;
        if (elementType is not null)
        {
            row.TypeName = elementType.Name ?? string.Empty;
            row.Family = elementType.FamilyName ?? string.Empty;
            AddParameters(row, elementType, "Type");
        }

        if (element is FamilyInstance familyInstance)
        {
            if (string.IsNullOrWhiteSpace(row.Family))
                row.Family = familyInstance.Symbol?.FamilyName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(row.TypeName))
                row.TypeName = familyInstance.Symbol?.Name ?? string.Empty;
        }

        AddParameters(row, element, "Instance");
        return row;
    }

    private static void AddParameters(ParameterTableRow row, Element element, string scope)
    {
        foreach (Parameter parameter in element.Parameters)
        {
            if (parameter?.Definition is null)
                continue;

            string name = parameter.Definition.Name;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            string key = scope + "." + name;
            if (row.Parameters.ContainsKey(key))
                continue;

            string? value = TryGetDisplayValue(parameter);
            if (value is null)
                continue;

            row.Parameters[key] = value;
        }
    }

    private static string? TryGetDisplayValue(Parameter parameter)
    {
        try
        {
            if (!parameter.HasValue)
                return string.Empty;

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return parameter.AsString() ?? parameter.AsValueString() ?? string.Empty;
                case StorageType.Integer:
                    return parameter.AsValueString() ?? parameter.AsInteger().ToString();
                case StorageType.Double:
                    return parameter.AsValueString() ?? parameter.AsDouble().ToString("G");
                case StorageType.ElementId:
                    ElementId id = parameter.AsElementId();
                    if (id == ElementId.InvalidElementId)
                        return string.Empty;
                    Element? referenced = parameter.Element?.Document.GetElement(id);
                    return referenced?.Name ?? id.Value.ToString();
                default:
                    return parameter.AsValueString() ?? string.Empty;
            }
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return null;
        }
    }

    private static string GetIfcGuid(Document document, Element element)
    {
        try
        {
            Guid exportId = ExportUtils.GetExportId(document, element.Id);
            return IfcGuid.From(exportId);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return element.UniqueId ?? string.Empty;
        }
    }

    private static string ElementName(Element element)
    {
        if (!string.IsNullOrWhiteSpace(element.Name))
            return element.Name;

        Parameter? mark = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
        string? markValue = mark?.AsString();
        if (!string.IsNullOrWhiteSpace(markValue))
            return markValue;

        return element.Category?.Name ?? element.Id.Value.ToString();
    }
}
