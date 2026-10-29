using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace MetroCIM.Services;

/// <summary>
/// Removes <c>IfcDistributionPort</c> flow-direction cones. Viewers such as Navisworks
/// draw those cones from the port entity even when it has no representation.
/// </summary>
public static class IfcPortGeometryStripper
{
    private static readonly Regex EntityLine = new(
        @"^#(\d+)\s*=\s*([A-Z][A-Z0-9]*)\s*\((.*)\)\s*;\s*$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly HashSet<string> RelationshipsToDeleteIfTheyReferenceAPort =
    [
        "IFCRELCONNECTSPORTTOELEMENT",
        "IFCRELCONNECTSPORTS"
    ];

    private static readonly HashSet<string> RelationshipsWithObjectLists =
    [
        "IFCRELNESTS",
        "IFCRELDEFINESBYPROPERTIES",
        "IFCRELASSOCIATESMATERIAL",
        "IFCRELASSIGNSTOGROUP",
        "IFCRELCONTAINEDINSPATIALSTRUCTURE",
        "IFCRELAGGREGATES"
    ];

    public static int Apply(string path)
    {
        if (!File.Exists(path) ||
            Path.GetExtension(path).Equals(".ifcxml", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (Path.GetExtension(path).Equals(".ifczip", StringComparison.OrdinalIgnoreCase))
            return ApplyZip(path);

        return PatchFile(path);
    }

    public static string PatchText(string ifcText)
    {
        var lines = MergeEntityLines(ifcText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
        var portIds = new HashSet<int>();
        var parsed = new List<(int Index, int Id, string Type, string Inner)?>();

        for (int i = 0; i < lines.Count; i++)
        {
            Match match = EntityLine.Match(lines[i].Trim());
            if (!match.Success)
            {
                parsed.Add(null);
                continue;
            }

            int id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            string type = match.Groups[2].Value.ToUpperInvariant();
            parsed.Add((i, id, type, match.Groups[3].Value));
            if (type == "IFCDISTRIBUTIONPORT")
                portIds.Add(id);
        }

        if (portIds.Count == 0)
            return ifcText;

        var remove = new HashSet<int>();
        for (int i = 0; i < parsed.Count; i++)
        {
            if (parsed[i] is not { } entity)
                continue;

            if (entity.Type == "IFCDISTRIBUTIONPORT")
            {
                remove.Add(i);
                continue;
            }

            if (RelationshipsToDeleteIfTheyReferenceAPort.Contains(entity.Type) &&
                ReferencesAny(entity.Inner, portIds))
            {
                remove.Add(i);
                continue;
            }

            if (!RelationshipsWithObjectLists.Contains(entity.Type))
                continue;

            IReadOnlyList<string> args = IfcColorPatcher.SplitArgs(entity.Inner);
            int listIndex = FindLastRefList(args);
            if (listIndex < 0)
                continue;

            string? filtered = FilterRefList(args[listIndex], portIds);
            if (filtered is null)
            {
                remove.Add(i);
                continue;
            }

            if (filtered == args[listIndex])
                continue;

            var rewritten = args.ToList();
            rewritten[listIndex] = filtered;
            lines[entity.Index] = $"#{entity.Id}={entity.Type}({string.Join(",", rewritten)});";
        }

        if (remove.Count == 0 && lines.SequenceEqual(ifcText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')))
            return ifcText;

        var kept = new List<string>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            if (!remove.Contains(i))
                kept.Add(lines[i]);
        }

        string newline = ifcText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return string.Join(newline, kept);
    }

    private static List<string> MergeEntityLines(IReadOnlyList<string> lines)
    {
        var merged = new List<string>(lines.Count);
        string? pending = null;
        foreach (string line in lines)
        {
            if (pending is null)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith('#') && trimmed.Contains('=') && !trimmed.EndsWith(';'))
                {
                    pending = trimmed;
                    continue;
                }

                merged.Add(line);
                continue;
            }

            pending += line.Trim();
            if (pending.EndsWith(';'))
            {
                merged.Add(pending);
                pending = null;
            }
        }

        if (pending is not null)
            merged.Add(pending);

        return merged;
    }

    private static bool ReferencesAny(string inner, HashSet<int> ids)
    {
        foreach (Match match in Regex.Matches(inner, @"#(\d+)"))
        {
            if (ids.Contains(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)))
                return true;
        }

        return false;
    }

    private static int FindLastRefList(IReadOnlyList<string> args)
    {
        for (int i = args.Count - 1; i >= 0; i--)
        {
            string arg = args[i].Trim();
            if (arg.StartsWith('(') && arg.EndsWith(')') && arg.Contains('#'))
                return i;
        }

        return -1;
    }

    private static string? FilterRefList(string listArg, HashSet<int> ports)
    {
        string inner = listArg.Trim();
        if (inner.StartsWith('(') && inner.EndsWith(')'))
            inner = inner[1..^1];

        var kept = new List<string>();
        foreach (string part in IfcColorPatcher.SplitArgs(inner))
        {
            string token = part.Trim();
            if (token.StartsWith('#') &&
                int.TryParse(token.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) &&
                ports.Contains(id))
            {
                continue;
            }

            kept.Add(part);
        }

        if (kept.Count == 0)
            return null;

        return "(" + string.Join(",", kept) + ")";
    }

    private static int ApplyZip(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        ZipArchiveEntry? entry = zip.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return 0;

        string temp = Path.Combine(Path.GetTempPath(), "MetroCIM-ports-" + Guid.NewGuid().ToString("N") + ".ifc");
        entry.ExtractToFile(temp, overwrite: true);
        try
        {
            int patched = PatchFile(temp);
            if (patched == 0)
                return 0;

            string name = entry.FullName;
            entry.Delete();
            zip.CreateEntryFromFile(temp, name);
            return patched;
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static int PatchFile(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Encoding encoding = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? Encoding.UTF8
            : Encoding.GetEncoding(28591);
        string original = encoding.GetString(bytes);
        string patched = PatchText(original);
        if (patched == original)
            return 0;

        File.WriteAllBytes(path, encoding.GetBytes(patched));
        return 1;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
