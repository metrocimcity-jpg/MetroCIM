namespace MetroCIM.Navisworks.Services;

public sealed class ExportedItem
{
    public ExportedItem(string id, string name, string type, IReadOnlyDictionary<string, string> properties)
    {
        Id = id;
        Name = name;
        Type = type;
        Properties = properties;
    }

    public string Id { get; }
    public string Name { get; }
    public string Type { get; }
    public IReadOnlyDictionary<string, string> Properties { get; }
}
