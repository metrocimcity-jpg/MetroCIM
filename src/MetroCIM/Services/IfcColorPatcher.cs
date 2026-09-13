using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MetroCIM.Models;

namespace MetroCIM.Services;

public static class IfcColorPatcher
{
    private static readonly Regex EntityLine = new(
        @"^#(\d+)\s*=\s*([A-Z][A-Z0-9]*)\s*\((.*)\)\s*;\s*$",
        RegexOptions.Compiled);

    public static int Apply(string path, IReadOnlyDictionary<string, ResolvedAppearance> colorsByIfcGuid)
    {
        if (colorsByIfcGuid.Count == 0 ||
            !File.Exists(path) ||
            Path.GetExtension(path).Equals(".ifcxml", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (Path.GetExtension(path).Equals(".ifczip", StringComparison.OrdinalIgnoreCase))
            return ApplyZip(path, colorsByIfcGuid);

        return PatchFile(path, colorsByIfcGuid);
    }

    public static string PatchText(string ifcText, IReadOnlyDictionary<string, ResolvedAppearance> colorsByIfcGuid)
    {
        var entities = new Dictionary<int, Entity>();
        var lines = ifcText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        int maxId = 0;
        bool ifc2x3 = ifcText.Contains("IFC2X3", StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < lines.Count; i++)
        {
            Match match = EntityLine.Match(lines[i].Trim());
            if (!match.Success)
                continue;

            int id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            maxId = Math.Max(maxId, id);
            entities[id] = new Entity(id, match.Groups[2].Value, match.Groups[3].Value, i);
        }

        var styledByItem = new Dictionary<int, int>();
        foreach (Entity entity in entities.Values)
        {
            if (entity.Type != "IFCSTYLEDITEM")
                continue;

            IReadOnlyList<string> styledArgs = SplitArgs(entity.Inner);
            if (styledArgs.Count > 0 && TryParseRef(styledArgs[0], out int target) && target != 0)
                styledByItem[target] = entity.Id;
        }

        var styleByColor = new Dictionary<(byte, byte, byte, byte), int>();
        var materialByColor = new Dictionary<(byte, byte, byte, byte), int>();
        var replacements = new Dictionary<int, string>();
        var additions = new List<string>();
        int nextId = maxId;
        int ownerHistoryId = entities.Values.FirstOrDefault(e => e.Type == "IFCOWNERHISTORY").Id;
        if (ownerHistoryId == 0)
            ownerHistoryId = 1;

        var work = new List<(Entity Product, ResolvedAppearance Appearance, HashSet<int> Targets)>();
        foreach (Entity entity in entities.Values)
        {
            if (entity.Type.EndsWith("TYPE", StringComparison.Ordinal))
                continue;

            IReadOnlyList<string> args = SplitArgs(entity.Inner);
            if (args.Count < 6 ||
                !TryResolveAppearance(args, colorsByIfcGuid, out ResolvedAppearance appearance) ||
                !TryFindRepresentation(args, entities, out int representationId))
            {
                continue;
            }

            var targets = new HashSet<int>();
            CollectStyleTargets(entities, representationId, targets, []);
            work.Add((entity, appearance, targets));
        }

        var usage = new Dictionary<int, int>();
        foreach ((Entity _, ResolvedAppearance _, HashSet<int> targets) in work)
        {
            foreach (int target in targets)
                usage[target] = usage.GetValueOrDefault(target) + 1;
        }

        var patchedProducts = new HashSet<int>();
        foreach ((Entity product, ResolvedAppearance appearance, HashSet<int> targets) in work)
        {
            int styleId = GetOrCreateStyle(appearance, ifc2x3, styleByColor, additions, ref nextId);
            foreach (int target in targets)
            {
                if (usage.GetValueOrDefault(target) != 1)
                    continue;

                if (styledByItem.TryGetValue(target, out int existingId) &&
                    entities.TryGetValue(existingId, out Entity existing))
                {
                    replacements[existing.LineIndex] = $"#{existingId}=IFCSTYLEDITEM(#{target},(#{styleId}),$);";
                }
                else
                {
                    nextId++;
                    additions.Add($"#{nextId}=IFCSTYLEDITEM(#{target},(#{styleId}),$);");
                }
            }

            patchedProducts.Add(product.Id);
        }

        if (patchedProducts.Count == 0)
            return ifcText;

        foreach (Entity entity in entities.Values)
        {
            if (entity.Type != "IFCRELASSOCIATESMATERIAL")
                continue;

            IReadOnlyList<string> args = SplitArgs(entity.Inner);
            if (args.Count < 6)
                continue;

            List<int> related = ParseRefs(args[4]).Where(id => !patchedProducts.Contains(id)).ToList();
            IReadOnlyList<int> original = ParseRefs(args[4]).ToList();
            if (related.Count == original.Count)
                continue;

            string relatedArg = related.Count == 0 ? "()" : "(" + string.Join(",", related.Select(id => "#" + id)) + ")";
            replacements[entity.LineIndex] =
                $"#{entity.Id}=IFCRELASSOCIATESMATERIAL({args[0]},{args[1]},{args[2]},{args[3]},{relatedArg},{args[5]});";
        }

        foreach (IGrouping<(byte R, byte G, byte B, byte A), (Entity Product, ResolvedAppearance Appearance, HashSet<int> Targets)> group in
                 work.GroupBy(item => (item.Appearance.R, item.Appearance.G, item.Appearance.B, item.Appearance.A)))
        {
            int materialId = GetOrCreateMaterial(group.Key, group.First().Appearance, materialByColor, additions, ref nextId);
            string related = "(" + string.Join(",", group.Select(item => "#" + item.Product.Id)) + ")";
            nextId++;
            additions.Add(
                $"#{nextId}=IFCRELASSOCIATESMATERIAL('{IfcGuid.NewIfcGlobalId()}',#{ownerHistoryId},$,$,{related},#{materialId});");
        }

        foreach ((int lineIndex, string replacement) in replacements)
            lines[lineIndex] = replacement;

        int endSec = lines.FindLastIndex(line =>
            line.Trim().Equals("ENDSEC;", StringComparison.OrdinalIgnoreCase));
        if (endSec < 0)
            lines.AddRange(additions);
        else
            lines.InsertRange(endSec, additions);

        return string.Join("\n", lines) + (ifcText.EndsWith('\n') || ifcText.EndsWith("\r\n") ? "\n" : string.Empty);
    }

    public static IReadOnlyList<string> SplitArgs(string inner)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        int depth = 0;
        bool inString = false;

        for (int i = 0; i < inner.Length; i++)
        {
            char c = inner[i];
            if (c == '\'' && (i == 0 || inner[i - 1] != '\\'))
                inString = !inString;

            if (!inString)
            {
                if (c == '(')
                    depth++;
                else if (c == ')')
                    depth--;
                else if (c == ',' && depth == 0)
                {
                    args.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }
            }

            current.Append(c);
        }

        if (current.Length > 0)
            args.Add(current.ToString().Trim());

        return args;
    }

    private static int ApplyZip(string path, IReadOnlyDictionary<string, ResolvedAppearance> colorsByIfcGuid)
    {
        using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update);
        System.IO.Compression.ZipArchiveEntry? entry = zip.Entries.FirstOrDefault(e =>
            e.Name.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return 0;

        string temp = Path.Combine(Path.GetTempPath(), "MetroCIM-" + Guid.NewGuid().ToString("N") + ".ifc");
        entry.ExtractToFile(temp, overwrite: true);
        try
        {
            int patched = PatchFile(temp, colorsByIfcGuid);
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

    private static int PatchFile(string path, IReadOnlyDictionary<string, ResolvedAppearance> colorsByIfcGuid)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Encoding encoding = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? Encoding.UTF8
            : Encoding.GetEncoding(28591);
        string original = encoding.GetString(bytes);
        string patched = PatchText(original, colorsByIfcGuid);
        if (ReferenceEquals(patched, original) || patched == original)
            return 0;

        File.WriteAllBytes(path, encoding.GetBytes(patched));
        return 1;
    }

    private static int GetOrCreateStyle(
        ResolvedAppearance appearance,
        bool ifc2x3,
        Dictionary<(byte, byte, byte, byte), int> cache,
        List<string> additions,
        ref int nextId)
    {
        var key = (appearance.R, appearance.G, appearance.B, appearance.A);
        if (cache.TryGetValue(key, out int cached))
            return cached;

        string r = IfcReal(appearance.R / 255d);
        string g = IfcReal(appearance.G / 255d);
        string b = IfcReal(appearance.B / 255d);
        string transparency = IfcReal((255 - appearance.A) / 255d);

        nextId++;
        int colorId = nextId;
        additions.Add($"#{colorId}=IFCCOLOURRGB($,{r},{g},{b});");

        nextId++;
        int renderingId = nextId;
        additions.Add(
            $"#{renderingId}=IFCSURFACESTYLERENDERING(#{colorId},{transparency},$,$,$,$,$,.NOTDEFINED.);");

        nextId++;
        int surfaceId = nextId;
        additions.Add($"#{surfaceId}=IFCSURFACESTYLE('MetroCIM',.BOTH.,(#{renderingId}));");

        int styleId = surfaceId;
        if (ifc2x3)
        {
            nextId++;
            styleId = nextId;
            additions.Add($"#{styleId}=IFCPRESENTATIONSTYLEASSIGNMENT((#{surfaceId}));");
        }

        cache[key] = styleId;
        return styleId;
    }

    private static void CollectStyleTargets(
        Dictionary<int, Entity> entities,
        int id,
        HashSet<int> targets,
        HashSet<int> visited)
    {
        if (!visited.Add(id) || !entities.TryGetValue(id, out Entity entity))
            return;

        IReadOnlyList<string> args = SplitArgs(entity.Inner);
        switch (entity.Type)
        {
            case "IFCPRODUCTDEFINITIONSHAPE":
                if (args.Count > 0)
                {
                    foreach (int nested in ParseRefs(args[^1]))
                        CollectStyleTargets(entities, nested, targets, visited);
                }
                break;

            case "IFCSHAPEREPRESENTATION":
                if (args.Count >= 2)
                {
                    string identifier = Unquote(args[1]);
                    if (identifier is "Axis" or "Box" or "Annotation" or "FootPrint" or "Profile")
                        return;
                }

                if (args.Count > 0)
                {
                    foreach (int nested in ParseRefs(args[^1]))
                        AddTarget(entities, nested, targets, visited);
                }
                break;

            default:
                AddTarget(entities, id, targets, visited);
                break;
        }
    }

    private static void AddTarget(
        Dictionary<int, Entity> entities,
        int id,
        HashSet<int> targets,
        HashSet<int> visited)
    {
        if (!entities.TryGetValue(id, out Entity entity))
            return;

        IReadOnlyList<string> args = SplitArgs(entity.Inner);
        if (entity.Type == "IFCSTYLEDITEM")
        {
            if (args.Count > 0 && TryParseRef(args[0], out int target) && target != 0)
                AddTarget(entities, target, targets, visited);
            return;
        }

        if (entity.Type is "IFCBOOLEANRESULT" or "IFCBOOLEANCLIPPINGRESULT")
        {
            targets.Add(id);
            return;
        }

        targets.Add(id);
        _ = visited;
    }

    private static IEnumerable<int> ParseRefs(string token)
    {
        foreach (string part in SplitArgs(UnwrapList(token)))
        {
            if (TryParseRef(part, out int id) && id != 0)
                yield return id;
        }
    }

    private static string UnwrapList(string token)
    {
        token = token.Trim();
        if (token.StartsWith('(') && token.EndsWith(')'))
            return token[1..^1];
        return token;
    }

    private static int GetOrCreateMaterial(
        (byte R, byte G, byte B, byte A) key,
        ResolvedAppearance appearance,
        Dictionary<(byte, byte, byte, byte), int> cache,
        List<string> additions,
        ref int nextId)
    {
        if (cache.TryGetValue(key, out int cached))
            return cached;

        nextId++;
        cache[key] = nextId;
        additions.Add($"#{nextId}=IFCMATERIAL('{IfcAppearanceApplier.MaterialName(appearance)}');");
        return nextId;
    }

    private static bool TryResolveAppearance(
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, ResolvedAppearance> colorsByIfcGuid,
        out ResolvedAppearance appearance)
    {
        appearance = default;
        foreach (string arg in args)
        {
            string value = Unquote(arg);
            if (value.Length == 0)
                continue;
            if (colorsByIfcGuid.TryGetValue(value, out appearance))
                return true;
        }

        return false;
    }

    private static bool TryFindRepresentation(
        IReadOnlyList<string> args,
        Dictionary<int, Entity> entities,
        out int representationId)
    {
        foreach (string arg in args)
        {
            if (!TryParseRef(arg, out int id) || id == 0 || !entities.TryGetValue(id, out Entity entity))
                continue;

            if (entity.Type is "IFCPRODUCTDEFINITIONSHAPE" or "IFCSHAPEREPRESENTATION")
            {
                representationId = id;
                return true;
            }
        }

        representationId = 0;
        return false;
    }

    private static bool TryParseGuid(string token, out string guid)
    {
        guid = Unquote(token);
        return guid.Length == 22;
    }

    private static bool TryParseRef(string token, out int id)
    {
        token = token.Trim();
        if (token == "$" || token.Length < 2 || token[0] != '#')
        {
            id = 0;
            return false;
        }

        return int.TryParse(token.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }

    private static string Unquote(string token)
    {
        token = token.Trim();
        if (token.Length >= 2 && token[0] == '\'' && token[^1] == '\'')
            return token[1..^1].Replace("''", "'");
        return token;
    }

    private static string IfcReal(double value)
    {
        string text = value.ToString("0.######", CultureInfo.InvariantCulture);
        return text.Contains('.') ? text : text + ".";
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

    private readonly record struct Entity(int Id, string Type, string Inner, int LineIndex);
}
