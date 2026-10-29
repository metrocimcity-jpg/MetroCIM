using Autodesk.Navisworks.Api;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MetroCIM.Navisworks.Services;

public sealed class MetadataExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public void Write(string path, Document document, IReadOnlyList<ExportedItem> items)
    {
        string modelId = SanitizeId(string.IsNullOrWhiteSpace(document.Title) ? "Model" : document.Title);
        var metaObjects = new List<XeokitMetaObject>
        {
            new XeokitMetaObject
            {
                Id = modelId,
                Name = string.IsNullOrWhiteSpace(document.Title) ? "Model" : document.Title,
                Type = "IfcProject",
                Parent = null
            }
        };

        var propertySets = new List<XeokitPropertySet>();
        foreach (ExportedItem item in items)
        {
            string psetId = "pset-" + item.Id;
            propertySets.Add(new XeokitPropertySet
            {
                Id = psetId,
                Name = "Identity Data",
                Type = "Default",
                Properties = item.Properties
                    .Select(pair => new XeokitProperty { Name = pair.Key, Value = pair.Value })
                    .ToList()
            });

            metaObjects.Add(new XeokitMetaObject
            {
                Id = item.Id,
                Name = item.Name,
                Type = item.Type,
                Parent = modelId,
                PropertySetIds = new List<string> { psetId }
            });
        }

        var model = new XeokitMetaModel
        {
            Id = modelId,
            ProjectId = modelId,
            Author = string.Empty,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            Schema = "Navisworks",
            CreatingApplication = "MetroCIM",
            MetaObjects = metaObjects,
            PropertySets = propertySets
        };

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, JsonSerializer.Serialize(model, JsonOptions), Encoding.UTF8);
    }

    private static string SanitizeId(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return chars.Length == 0 ? "Model" : new string(chars);
    }

    private sealed class XeokitMetaModel
    {
        public string? Id { get; set; }
        public string? ProjectId { get; set; }
        public string? Author { get; set; }
        public string? CreatedAt { get; set; }
        public string? Schema { get; set; }
        public string? CreatingApplication { get; set; }
        public List<XeokitMetaObject>? MetaObjects { get; set; }
        public List<XeokitPropertySet>? PropertySets { get; set; }
    }

    private sealed class XeokitMetaObject
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Parent { get; set; }
        public List<string>? PropertySetIds { get; set; }
    }

    private sealed class XeokitPropertySet
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public List<XeokitProperty>? Properties { get; set; }
    }

    private sealed class XeokitProperty
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
    }
}
