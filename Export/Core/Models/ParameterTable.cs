namespace MetroCIM.Models;

/// <summary>
/// One exported instance row: identity columns plus instance/type parameter values.
/// </summary>
public sealed class ParameterTableRow
{
    public string RevitId { get; set; } = string.Empty;
    public string IfcGuid { get; set; } = string.Empty;
    public string UniqueId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ParameterTable
{
    public static readonly string[] IdentityHeaders =
    [
        "RevitId",
        "IfcGuid",
        "UniqueId",
        "Category",
        "Family",
        "Type",
        "Name"
    ];

    public List<ParameterTableRow> Rows { get; } = new();

    public IReadOnlyList<string> BuildHeaders()
    {
        var extras = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ParameterTableRow row in Rows)
        {
            foreach (string key in row.Parameters.Keys)
                extras.Add(key);
        }

        var headers = new List<string>(IdentityHeaders.Length + extras.Count);
        headers.AddRange(IdentityHeaders);
        headers.AddRange(extras);
        return headers;
    }

    public IEnumerable<IReadOnlyList<string>> EnumerateCells(IReadOnlyList<string> headers)
    {
        foreach (ParameterTableRow row in Rows)
        {
            var cells = new string[headers.Count];
            for (int i = 0; i < headers.Count; i++)
            {
                string header = headers[i];
                cells[i] = header switch
                {
                    "RevitId" => row.RevitId,
                    "IfcGuid" => row.IfcGuid,
                    "UniqueId" => row.UniqueId,
                    "Category" => row.Category,
                    "Family" => row.Family,
                    "Type" => row.TypeName,
                    "Name" => row.Name,
                    _ => row.Parameters.TryGetValue(header, out string? value) ? value : string.Empty
                };
            }

            yield return cells;
        }
    }
}
