namespace MetroCIM.Services;

public static class IfcSetupName
{
    public static string? Resolve(IReadOnlyList<string> names, string? lastUsed)
    {
        if (names.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(lastUsed))
        {
            foreach (string name in names)
            {
                if (string.Equals(name, lastUsed, StringComparison.OrdinalIgnoreCase))
                    return name;
            }
        }

        foreach (string name in names)
        {
            if (name.Contains("In-Session", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("In Session", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return names[0];
    }
}
