using Autodesk.Revit.DB;
using MetroCIM.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MetroCIM.Services;

public sealed class MetadataExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly BuiltInParameter[] IdentityParameters =
    [
        BuiltInParameter.ALL_MODEL_MARK,
        BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS,
        BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM,
        BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM,
        BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM,
        BuiltInParameter.RBS_SYSTEM_NAME_PARAM,
        BuiltInParameter.RBS_PIPE_DIAMETER_PARAM,
        BuiltInParameter.CURVE_ELEM_LENGTH
    ];

    public void Write(string path, Document document, View3D view, IReadOnlyList<ElementGeometry> elements) =>
        Write(path, document, view, elements.Select(e => new ExportedElement(e.Id, e.Element)).ToList());

    public void Write(string path, Document document, View3D view, IReadOnlyList<ExportedElement> elements)
    {
        string modelId = SanitizeId(document.Title);
        var metaObjects = new List<XeokitMetaObject>
        {
            new()
            {
                Id = modelId,
                Name = string.IsNullOrWhiteSpace(document.Title) ? view.Name : document.Title,
                Type = "IfcProject",
                Parent = null
            }
        };

        var propertySets = new List<XeokitPropertySet>();
        var levelIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (ExportedElement item in elements)
        {
            Element element = item.Element;
            string? levelId = TryGetLevelId(element, modelId, metaObjects, levelIds);
            string psetId = $"pset-{item.Id}";

            propertySets.Add(new XeokitPropertySet
            {
                Id = psetId,
                Name = "Identity Data",
                Type = "Default",
                Properties = BuildProperties(element)
            });

            metaObjects.Add(new XeokitMetaObject
            {
                Id = item.Id,
                Name = ElementName(element),
                Type = element.Category?.Name ?? element.GetType().Name,
                Parent = levelId ?? modelId,
                PropertySetIds = [psetId]
            });
        }

        var model = new XeokitMetaModel
        {
            Id = modelId,
            ProjectId = modelId,
            Author = document.ProjectInformation?.Author ?? string.Empty,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            Schema = "Revit",
            CreatingApplication = "MetroCIM",
            MetaObjects = metaObjects,
            PropertySets = propertySets
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(model, JsonOptions));
    }

    private static string? TryGetLevelId(
        Element element,
        string modelId,
        List<XeokitMetaObject> metaObjects,
        HashSet<string> levelIds)
    {
        ElementId levelElementId = element.LevelId;
        if (levelElementId == ElementId.InvalidElementId)
            return null;

        if (element.Document.GetElement(levelElementId) is not Level level)
            return null;

        string id = SanitizeId(level.UniqueId);
        if (levelIds.Add(id))
        {
            metaObjects.Add(new XeokitMetaObject
            {
                Id = id,
                Name = level.Name,
                Type = "IfcBuildingStorey",
                Parent = modelId
            });
        }

        return id;
    }

    private static List<XeokitProperty> BuildProperties(Element element)
    {
        var properties = new List<XeokitProperty>
        {
            new() { Name = "ElementId", Value = element.Id.Value.ToString(), Type = "IfcIdentifier" },
            new() { Name = "UniqueId", Value = element.UniqueId, Type = "IfcGloballyUniqueId" }
        };

        if (element.Category is not null)
            properties.Add(new() { Name = "Category", Value = element.Category.Name, Type = "IfcLabel" });

        if (element.Document.GetElement(element.GetTypeId()) is ElementType type)
        {
            properties.Add(new() { Name = "Type", Value = type.Name, Type = "IfcLabel" });
            if (!string.IsNullOrWhiteSpace(type.FamilyName))
                properties.Add(new() { Name = "Family", Value = type.FamilyName, Type = "IfcLabel" });
        }

        foreach (BuiltInParameter builtIn in IdentityParameters)
            TryAddParameter(element, builtIn, properties);

        return properties;
    }

    private static void TryAddParameter(Element element, BuiltInParameter builtIn, List<XeokitProperty> properties)
    {
        try
        {
            Parameter? parameter = element.get_Parameter(builtIn);
            if (parameter is null || !parameter.HasValue)
                return;

            string? value = parameter.StorageType == StorageType.String
                ? parameter.AsString()
                : parameter.AsValueString();

            if (string.IsNullOrWhiteSpace(value))
                return;

            string name = parameter.Definition?.Name ?? builtIn.ToString();
            if (properties.Exists(p => p.Name == name))
                return;

            properties.Add(new XeokitProperty
            {
                Name = name,
                Value = value,
                Type = "IfcLabel"
            });
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            // Parameter is not available on this element.
        }
    }

    private static string ElementName(Element element)
    {
        if (!string.IsNullOrWhiteSpace(element.Name))
            return element.Name;

        if (element.Document.GetElement(element.GetTypeId()) is ElementType type && !string.IsNullOrWhiteSpace(type.Name))
            return type.Name;

        return element.Category?.Name ?? element.Id.Value.ToString();
    }

    internal static string SanitizeId(string value)
    {
        value = string.IsNullOrWhiteSpace(value) ? "model" : value.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value;
    }
}

internal sealed class XeokitMetaModel
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Author { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string Schema { get; set; } = "";
    public string CreatingApplication { get; set; } = "";
    public List<XeokitMetaObject> MetaObjects { get; set; } = [];
    public List<XeokitPropertySet> PropertySets { get; set; } = [];
}

internal sealed class XeokitMetaObject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Parent { get; set; }
    public List<string>? PropertySetIds { get; set; }
}

internal sealed class XeokitPropertySet
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public List<XeokitProperty> Properties { get; set; } = [];
}

internal sealed class XeokitProperty
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Type { get; set; } = "";
}
