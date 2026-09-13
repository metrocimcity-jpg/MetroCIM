using System.Globalization;
using System.IO.Compression;
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

        var definedByType = new Dictionary<int, int>();
        foreach (Entity entity in entities.Values)
        {
            if (entity.Type != "IFCRELDEFINESBYTYPE")
                continue;

            IReadOnlyList<string> args = SplitArgs(entity.Inner);
            if (args.Count < 6)
                continue;

            if (!TryParseRef(args[5], out int typeId) || typeId == 0)
                continue;

            foreach (int productId in ParseRefs(args[4]))
                definedByType[productId] = typeId;
        }

        var styleByColor = new Dictionary<(byte, byte, byte, byte), int>();
        var materialByColor = new Dictionary<(byte, byte, byte, byte), int>();
        var replacements = new Dictionary<int, string>();
        var additions = new List<string>();
        int nextId = maxId;
        int ownerHistoryId = entities.Values.FirstOrDefault(e => e.Type == "IFCOWNERHISTORY").Id;
        if (ownerHistoryId == 0)
            ownerHistoryId = 1;

        var work = new List<ProductWork>();
        foreach (Entity entity in entities.Values)
        {
            if (entity.Type.EndsWith("TYPE", StringComparison.Ordinal))
                continue;

            IReadOnlyList<string> args = SplitArgs(entity.Inner);
            if (args.Count < 6 ||
                !TryResolveAppearance(args, colorsByIfcGuid, out ResolvedAppearance appearance))
            {
                continue;
            }

            TryFindRepresentation(args, entities, out int representationId);
            definedByType.TryGetValue(entity.Id, out int typeId);
            work.Add(new ProductWork(entity, appearance, representationId, typeId, args));
        }

        InheritNestedAppearances(entities, definedByType, work);

        var colorsByRoot = new Dictionary<int, HashSet<(byte, byte, byte, byte)>>();
        foreach (ProductWork item in work)
        {
            foreach (int root in CollectGeometryRoots(entities, item.RepresentationId, item.TypeId, []))
            {
                if (!colorsByRoot.TryGetValue(root, out HashSet<(byte, byte, byte, byte)>? colors))
                {
                    colors = [];
                    colorsByRoot[root] = colors;
                }

                colors.Add((item.Appearance.R, item.Appearance.G, item.Appearance.B, item.Appearance.A));
            }
        }

        var mapBySourceAndColor = new Dictionary<(int MapId, byte R, byte G, byte B, byte A), int>();
        var cloneByRootAndColor = new Dictionary<(int RootId, byte R, byte G, byte B, byte A), int>();
        var patchedProducts = new HashSet<int>();

        foreach (ProductWork item in work)
        {
            int styleId = GetOrCreateStyle(item.Appearance, ifc2x3, styleByColor, additions, ref nextId);
            var color = (item.Appearance.R, item.Appearance.G, item.Appearance.B, item.Appearance.A);

            int representationId = item.RepresentationId;
            if (representationId == 0)
            {
                representationId = TryCreateInstanceRepresentation(
                    item,
                    entities,
                    color,
                    styleId,
                    colorsByRoot,
                    mapBySourceAndColor,
                    cloneByRootAndColor,
                    styledByItem,
                    replacements,
                    additions,
                    ref nextId);
            }

            if (representationId != 0)
            {
                StyleRepresentation(
                    entities,
                    representationId,
                    color,
                    styleId,
                    colorsByRoot,
                    mapBySourceAndColor,
                    cloneByRootAndColor,
                    styledByItem,
                    replacements,
                    additions,
                    ref nextId);
            }

            patchedProducts.Add(item.Product.Id);
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

        foreach (IGrouping<(byte R, byte G, byte B, byte A), ProductWork> group in
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
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        ZipArchiveEntry? entry = zip.Entries.FirstOrDefault(e =>
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

    private static void StyleRepresentation(
        Dictionary<int, Entity> entities,
        int id,
        (byte R, byte G, byte B, byte A) color,
        int styleId,
        Dictionary<int, HashSet<(byte, byte, byte, byte)>> colorsByRoot,
        Dictionary<(int MapId, byte R, byte G, byte B, byte A), int> mapBySourceAndColor,
        Dictionary<(int RootId, byte R, byte G, byte B, byte A), int> cloneByRootAndColor,
        Dictionary<int, int> styledByItem,
        Dictionary<int, string> replacements,
        List<string> additions,
        ref int nextId)
    {
        if (!entities.TryGetValue(id, out Entity entity))
            return;

        IReadOnlyList<string> args = SplitArgs(entity.Inner);
        switch (entity.Type)
        {
            case "IFCPRODUCTDEFINITIONSHAPE":
                if (args.Count == 0)
                    return;

                List<int> representations = ParseRefs(args[^1]).ToList();
                var newRepresentations = new List<int>();
                bool pdsChanged = false;
                foreach (int representation in representations)
                {
                    int styled = StyleShapeRepresentation(
                        entities,
                        representation,
                        color,
                        styleId,
                        colorsByRoot,
                        mapBySourceAndColor,
                        cloneByRootAndColor,
                        styledByItem,
                        replacements,
                        additions,
                        ref nextId);
                    newRepresentations.Add(styled);
                    pdsChanged |= styled != representation;
                }

                if (pdsChanged)
                {
                    var rewritten = args.ToList();
                    rewritten[^1] = "(" + string.Join(",", newRepresentations.Select(value => "#" + value)) + ")";
                    replacements[entity.LineIndex] = $"#{entity.Id}={entity.Type}({string.Join(",", rewritten)});";
                }

                break;

            case "IFCSHAPEREPRESENTATION":
                StyleShapeRepresentation(
                    entities,
                    id,
                    color,
                    styleId,
                    colorsByRoot,
                    mapBySourceAndColor,
                    cloneByRootAndColor,
                    styledByItem,
                    replacements,
                    additions,
                    ref nextId);
                break;
        }
    }

    private static int StyleShapeRepresentation(
        Dictionary<int, Entity> entities,
        int id,
        (byte R, byte G, byte B, byte A) color,
        int styleId,
        Dictionary<int, HashSet<(byte, byte, byte, byte)>> colorsByRoot,
        Dictionary<(int MapId, byte R, byte G, byte B, byte A), int> mapBySourceAndColor,
        Dictionary<(int RootId, byte R, byte G, byte B, byte A), int> cloneByRootAndColor,
        Dictionary<int, int> styledByItem,
        Dictionary<int, string> replacements,
        List<string> additions,
        ref int nextId)
    {
        if (!entities.TryGetValue(id, out Entity entity) || entity.Type != "IFCSHAPEREPRESENTATION")
            return id;

        IReadOnlyList<string> args = SplitArgs(entity.Inner);
        if (args.Count >= 2)
        {
            string identifier = Unquote(args[1]);
            if (identifier is "Axis" or "Box" or "Annotation" or "FootPrint" or "Profile")
                return id;
        }

        if (args.Count == 0)
            return id;

        List<int> items = ParseRefs(args[^1]).ToList();
        var newItems = new List<int>(items.Count);
        bool itemsChanged = false;
        foreach (int itemId in items)
        {
            int styled = StyleItem(
                entities,
                itemId,
                color,
                styleId,
                colorsByRoot,
                mapBySourceAndColor,
                cloneByRootAndColor,
                styledByItem,
                replacements,
                additions,
                ref nextId);
            newItems.Add(styled);
            itemsChanged |= styled != itemId;
        }

        if (!itemsChanged)
            return id;

        if (IsLineAlreadyRewritten(entity.LineIndex, replacements))
        {
            nextId++;
            var clonedArgs = args.ToList();
            clonedArgs[^1] = "(" + string.Join(",", newItems.Select(value => "#" + value)) + ")";
            additions.Add($"#{nextId}=IFCSHAPEREPRESENTATION({string.Join(",", clonedArgs)});");
            return nextId;
        }

        var rewritten = args.ToList();
        rewritten[^1] = "(" + string.Join(",", newItems.Select(value => "#" + value)) + ")";
        replacements[entity.LineIndex] = $"#{entity.Id}=IFCSHAPEREPRESENTATION({string.Join(",", rewritten)});";
        return id;
    }

    private static int StyleItem(
        Dictionary<int, Entity> entities,
        int id,
        (byte R, byte G, byte B, byte A) color,
        int styleId,
        Dictionary<int, HashSet<(byte, byte, byte, byte)>> colorsByRoot,
        Dictionary<(int MapId, byte R, byte G, byte B, byte A), int> mapBySourceAndColor,
        Dictionary<(int RootId, byte R, byte G, byte B, byte A), int> cloneByRootAndColor,
        Dictionary<int, int> styledByItem,
        Dictionary<int, string> replacements,
        List<string> additions,
        ref int nextId)
    {
        if (!entities.TryGetValue(id, out Entity entity))
            return id;

        IReadOnlyList<string> args = SplitArgs(entity.Inner);
        if (entity.Type == "IFCSTYLEDITEM")
        {
            if (args.Count > 0 && TryParseRef(args[0], out int target) && target != 0)
            {
                return StyleItem(
                    entities,
                    target,
                    color,
                    styleId,
                    colorsByRoot,
                    mapBySourceAndColor,
                    cloneByRootAndColor,
                    styledByItem,
                    replacements,
                    additions,
                    ref nextId);
            }

            return id;
        }

        if (entity.Type == "IFCMAPPEDITEM")
        {
            StyleMappedItem(
                entity,
                args,
                color,
                styleId,
                entities,
                colorsByRoot,
                mapBySourceAndColor,
                cloneByRootAndColor,
                styledByItem,
                replacements,
                additions,
                ref nextId);
            StyleInPlace(id, styleId, styledByItem, entities, replacements, additions, ref nextId);
            return id;
        }

        return AssignRoot(
            id,
            color,
            styleId,
            colorsByRoot,
            cloneByRootAndColor,
            styledByItem,
            entities,
            replacements,
            additions,
            ref nextId);
    }

    private static void StyleMappedItem(
        Entity mappedItem,
        IReadOnlyList<string> args,
        (byte R, byte G, byte B, byte A) color,
        int styleId,
        Dictionary<int, Entity> entities,
        Dictionary<int, HashSet<(byte, byte, byte, byte)>> colorsByRoot,
        Dictionary<(int MapId, byte R, byte G, byte B, byte A), int> mapBySourceAndColor,
        Dictionary<(int RootId, byte R, byte G, byte B, byte A), int> cloneByRootAndColor,
        Dictionary<int, int> styledByItem,
        Dictionary<int, string> replacements,
        List<string> additions,
        ref int nextId)
    {
        if (args.Count == 0 || !TryParseRef(args[0], out int mapId) || mapId == 0)
            return;

        bool splitColors = CollectMapRoots(entities, mapId, [], [])
            .Any(root => colorsByRoot.TryGetValue(root, out HashSet<(byte, byte, byte, byte)>? colors) &&
                         colors.Count > 1);

        int coloredMap = splitColors
            ? GetOrCreateColoredMap(
                mapId,
                color,
                styleId,
                entities,
                colorsByRoot,
                mapBySourceAndColor,
                cloneByRootAndColor,
                styledByItem,
                replacements,
                additions,
                ref nextId)
            : mapId;

        if (!splitColors)
        {
            foreach (int root in CollectMapRoots(entities, mapId, [], []))
            {
                AssignRoot(
                    root,
                    color,
                    styleId,
                    colorsByRoot,
                    cloneByRootAndColor,
                    styledByItem,
                    entities,
                    replacements,
                    additions,
                    ref nextId);
            }
        }

        if (coloredMap == mapId)
            return;

        var rewritten = args.ToList();
        rewritten[0] = "#" + coloredMap;
        replacements[mappedItem.LineIndex] = $"#{mappedItem.Id}=IFCMAPPEDITEM({string.Join(",", rewritten)});";
    }

    private static int GetOrCreateColoredMap(
        int mapId,
        (byte R, byte G, byte B, byte A) color,
        int styleId,
        Dictionary<int, Entity> entities,
        Dictionary<int, HashSet<(byte, byte, byte, byte)>> colorsByRoot,
        Dictionary<(int MapId, byte R, byte G, byte B, byte A), int> mapBySourceAndColor,
        Dictionary<(int RootId, byte R, byte G, byte B, byte A), int> cloneByRootAndColor,
        Dictionary<int, int> styledByItem,
        Dictionary<int, string> replacements,
        List<string> additions,
        ref int nextId)
    {
        var key = (mapId, color.R, color.G, color.B, color.A);
        if (mapBySourceAndColor.TryGetValue(key, out int cached))
            return cached;

        if (!entities.TryGetValue(mapId, out Entity map) || map.Type != "IFCREPRESENTATIONMAP")
            return mapId;

        IReadOnlyList<string> mapArgs = SplitArgs(map.Inner);
        if (mapArgs.Count < 2 || !TryParseRef(mapArgs[1], out int mappedRep) || mappedRep == 0)
            return mapId;

        int coloredRep = CloneShapeRepresentation(
            mappedRep,
            color,
            styleId,
            entities,
            colorsByRoot,
            mapBySourceAndColor,
            cloneByRootAndColor,
            styledByItem,
            replacements,
            additions,
            ref nextId);

        nextId++;
        int newMap = nextId;
        additions.Add($"#{newMap}=IFCREPRESENTATIONMAP({mapArgs[0]},#{coloredRep});");
        mapBySourceAndColor[key] = newMap;
        return newMap;
    }

    private static int CloneShapeRepresentation(
        int id,
        (byte R, byte G, byte B, byte A) color,
        int styleId,
        Dictionary<int, Entity> entities,
        Dictionary<int, HashSet<(byte, byte, byte, byte)>> colorsByRoot,
        Dictionary<(int MapId, byte R, byte G, byte B, byte A), int> mapBySourceAndColor,
        Dictionary<(int RootId, byte R, byte G, byte B, byte A), int> cloneByRootAndColor,
        Dictionary<int, int> styledByItem,
        Dictionary<int, string> replacements,
        List<string> additions,
        ref int nextId)
    {
        if (!entities.TryGetValue(id, out Entity entity) || entity.Type != "IFCSHAPEREPRESENTATION")
            return id;

        IReadOnlyList<string> args = SplitArgs(entity.Inner);
        if (args.Count == 0)
            return id;

        var newItems = new List<int>();
        foreach (int itemId in ParseRefs(args[^1]))
        {
            newItems.Add(StyleItem(
                entities,
                itemId,
                color,
                styleId,
                colorsByRoot,
                mapBySourceAndColor,
                cloneByRootAndColor,
                styledByItem,
                replacements,
                additions,
                ref nextId));
        }

        var clonedArgs = args.ToList();
        clonedArgs[^1] = "(" + string.Join(",", newItems.Select(value => "#" + value)) + ")";
        nextId++;
        additions.Add($"#{nextId}=IFCSHAPEREPRESENTATION({string.Join(",", clonedArgs)});");
        return nextId;
    }

    private static int AssignRoot(
        int rootId,
        (byte R, byte G, byte B, byte A) color,
        int styleId,
        Dictionary<int, HashSet<(byte, byte, byte, byte)>> colorsByRoot,
        Dictionary<(int RootId, byte R, byte G, byte B, byte A), int> cloneByRootAndColor,
        Dictionary<int, int> styledByItem,
        Dictionary<int, Entity> entities,
        Dictionary<int, string> replacements,
        List<string> additions,
        ref int nextId)
    {
        bool split = colorsByRoot.TryGetValue(rootId, out HashSet<(byte, byte, byte, byte)>? colors) &&
                     colors.Count > 1;
        if (!split)
        {
            StyleInPlace(rootId, styleId, styledByItem, entities, replacements, additions, ref nextId);
            return rootId;
        }

        var key = (rootId, color.R, color.G, color.B, color.A);
        if (cloneByRootAndColor.TryGetValue(key, out int cached))
            return cached;

        if (!entities.TryGetValue(rootId, out Entity root))
            return rootId;

        nextId++;
        int cloneId = nextId;
        additions.Add($"#{cloneId}={root.Type}({root.Inner});");
        cloneByRootAndColor[key] = cloneId;
        StyleInPlace(cloneId, styleId, styledByItem, entities, replacements, additions, ref nextId);
        return cloneId;
    }

    private static void StyleInPlace(
        int targetId,
        int styleId,
        Dictionary<int, int> styledByItem,
        Dictionary<int, Entity> entities,
        Dictionary<int, string> replacements,
        List<string> additions,
        ref int nextId)
    {
        if (styledByItem.TryGetValue(targetId, out int existingId) &&
            entities.TryGetValue(existingId, out Entity existing))
        {
            replacements[existing.LineIndex] = $"#{existingId}=IFCSTYLEDITEM(#{targetId},(#{styleId}),$);";
            return;
        }

        if (styledByItem.ContainsKey(targetId))
            return;

        nextId++;
        additions.Add($"#{nextId}=IFCSTYLEDITEM(#{targetId},(#{styleId}),$);");
        styledByItem[targetId] = nextId;
    }

    private static int TryCreateInstanceRepresentation(
        ProductWork item,
        Dictionary<int, Entity> entities,
        (byte R, byte G, byte B, byte A) color,
        int styleId,
        Dictionary<int, HashSet<(byte, byte, byte, byte)>> colorsByRoot,
        Dictionary<(int MapId, byte R, byte G, byte B, byte A), int> mapBySourceAndColor,
        Dictionary<(int RootId, byte R, byte G, byte B, byte A), int> cloneByRootAndColor,
        Dictionary<int, int> styledByItem,
        Dictionary<int, string> replacements,
        List<string> additions,
        ref int nextId)
    {
        if (item.TypeId == 0 || !entities.TryGetValue(item.TypeId, out Entity type))
            return 0;

        List<int> maps = FindRepresentationMaps(type, entities);
        if (maps.Count == 0)
            return 0;

        var coloredMaps = new List<int>(maps.Count);
        foreach (int mapId in maps)
        {
            coloredMaps.Add(GetOrCreateColoredMap(
                mapId,
                color,
                styleId,
                entities,
                colorsByRoot,
                mapBySourceAndColor,
                cloneByRootAndColor,
                styledByItem,
                replacements,
                additions,
                ref nextId));
        }

        if (!TryGetMapTransform(entities, maps[0], out string originRef, out string contextRef))
            return 0;

        nextId++;
        int transformId = nextId;
        additions.Add($"#{transformId}=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,{originRef},1.,$);");

        var mappedItems = new List<int>(coloredMaps.Count);
        foreach (int mapId in coloredMaps)
        {
            nextId++;
            additions.Add($"#{nextId}=IFCMAPPEDITEM(#{mapId},#{transformId});");
            mappedItems.Add(nextId);
        }

        nextId++;
        int shapeRepId = nextId;
        additions.Add(
            $"#{shapeRepId}=IFCSHAPEREPRESENTATION({contextRef},'Body','MappedRepresentation',({string.Join(",", mappedItems.Select(id => "#" + id))}));");

        nextId++;
        int pdsId = nextId;
        additions.Add($"#{pdsId}=IFCPRODUCTDEFINITIONSHAPE($,$,(#{shapeRepId}));");

        if (TryRewriteProductRepresentation(item, pdsId, entities, replacements))
            return pdsId;

        return 0;
    }

    private static bool TryRewriteProductRepresentation(
        ProductWork item,
        int pdsId,
        Dictionary<int, Entity> entities,
        Dictionary<int, string> replacements)
    {
        IReadOnlyList<string> args = item.Args;
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (!TryParseRef(args[i], out int id) || id == 0 || !entities.TryGetValue(id, out Entity entity))
                continue;

            if (entity.Type != "IFCLOCALPLACEMENT")
                continue;

            if (args[i + 1] != "$" &&
                !(TryParseRef(args[i + 1], out int existing) &&
                  entities.TryGetValue(existing, out Entity existingEntity) &&
                  existingEntity.Type is "IFCPRODUCTDEFINITIONSHAPE" or "IFCSHAPEREPRESENTATION"))
            {
                continue;
            }

            var rewritten = args.ToList();
            rewritten[i + 1] = "#" + pdsId;
            replacements[item.Product.LineIndex] =
                $"#{item.Product.Id}={item.Product.Type}({string.Join(",", rewritten)});";
            return true;
        }

        return false;
    }

    private static HashSet<int> CollectGeometryRoots(
        Dictionary<int, Entity> entities,
        int representationId,
        int typeId,
        HashSet<int> visited)
    {
        var roots = new HashSet<int>();
        if (representationId != 0)
            CollectRootsFrom(entities, representationId, roots, visited);

        if (typeId != 0 && entities.TryGetValue(typeId, out Entity type))
        {
            foreach (int mapId in FindRepresentationMaps(type, entities))
                CollectMapRoots(entities, mapId, roots, visited);
        }

        return roots;
    }

    private static void CollectRootsFrom(
        Dictionary<int, Entity> entities,
        int id,
        HashSet<int> roots,
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
                        CollectRootsFrom(entities, nested, roots, visited);
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
                        CollectItemRoots(entities, nested, roots, visited);
                }

                break;

            default:
                CollectItemRoots(entities, id, roots, visited);
                break;
        }
    }

    private static void CollectItemRoots(
        Dictionary<int, Entity> entities,
        int id,
        HashSet<int> roots,
        HashSet<int> visited)
    {
        if (!entities.TryGetValue(id, out Entity entity))
            return;

        IReadOnlyList<string> args = SplitArgs(entity.Inner);
        if (entity.Type == "IFCSTYLEDITEM")
        {
            if (args.Count > 0 && TryParseRef(args[0], out int target) && target != 0)
                CollectItemRoots(entities, target, roots, visited);
            return;
        }

        if (entity.Type == "IFCMAPPEDITEM")
        {
            if (args.Count > 0 && TryParseRef(args[0], out int mapId) && mapId != 0)
                CollectMapRoots(entities, mapId, roots, visited);
            return;
        }

        roots.Add(id);
    }

    private static HashSet<int> CollectMapRoots(
        Dictionary<int, Entity> entities,
        int mapId,
        HashSet<int> roots,
        HashSet<int> visited)
    {
        if (!entities.TryGetValue(mapId, out Entity map) || map.Type != "IFCREPRESENTATIONMAP")
            return roots;

        IReadOnlyList<string> args = SplitArgs(map.Inner);
        if (args.Count >= 2 && TryParseRef(args[1], out int mappedRep) && mappedRep != 0)
            CollectRootsFrom(entities, mappedRep, roots, visited);

        return roots;
    }

    private static List<int> FindRepresentationMaps(Entity type, Dictionary<int, Entity> entities)
    {
        foreach (string arg in SplitArgs(type.Inner))
        {
            List<int> refs = ParseRefs(arg).ToList();
            if (refs.Count == 0)
                continue;

            if (refs.All(id => entities.TryGetValue(id, out Entity entity) && entity.Type == "IFCREPRESENTATIONMAP"))
                return refs;
        }

        return [];
    }

    private static bool TryGetMapTransform(
        Dictionary<int, Entity> entities,
        int mapId,
        out string originRef,
        out string contextRef)
    {
        originRef = "$";
        contextRef = "$";
        if (!entities.TryGetValue(mapId, out Entity map))
            return false;

        IReadOnlyList<string> mapArgs = SplitArgs(map.Inner);
        if (mapArgs.Count < 2)
            return false;

        originRef = mapArgs[0];
        if (originRef.StartsWith('#') &&
            TryParseRef(originRef, out int originId) &&
            entities.TryGetValue(originId, out Entity origin))
        {
            IReadOnlyList<string> originArgs = SplitArgs(origin.Inner);
            if (originArgs.Count > 0 && originArgs[0].StartsWith('#'))
                originRef = originArgs[0];
        }

        if (TryParseRef(mapArgs[1], out int repId) &&
            entities.TryGetValue(repId, out Entity rep) &&
            SplitArgs(rep.Inner) is { Count: > 0 } repArgs)
        {
            contextRef = repArgs[0];
        }

        return originRef.StartsWith('#');
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

    private static void InheritNestedAppearances(
        Dictionary<int, Entity> entities,
        Dictionary<int, int> definedByType,
        List<ProductWork> work)
    {
        var nestedByParent = new Dictionary<int, List<int>>();
        foreach (Entity entity in entities.Values)
        {
            if (entity.Type is not ("IFCRELNESTS" or "IFCRELAGGREGATES"))
                continue;

            IReadOnlyList<string> args = SplitArgs(entity.Inner);
            if (args.Count < 6 || !TryParseRef(args[4], out int parentId) || parentId == 0)
                continue;

            if (!nestedByParent.TryGetValue(parentId, out List<int>? children))
            {
                children = [];
                nestedByParent[parentId] = children;
            }

            children.AddRange(ParseRefs(args[5]));
        }

        if (nestedByParent.Count == 0)
            return;

        var seen = work.Select(item => item.Product.Id).ToHashSet();
        for (int i = 0; i < work.Count; i++)
        {
            if (!nestedByParent.TryGetValue(work[i].Product.Id, out List<int>? children))
                continue;

            foreach (int childId in children)
            {
                if (!seen.Add(childId) || !entities.TryGetValue(childId, out Entity child))
                    continue;

                if (child.Type.EndsWith("TYPE", StringComparison.Ordinal))
                    continue;

                IReadOnlyList<string> childArgs = SplitArgs(child.Inner);
                TryFindRepresentation(childArgs, entities, out int representationId);
                definedByType.TryGetValue(childId, out int typeId);
                work.Add(new ProductWork(child, work[i].Appearance, representationId, typeId, childArgs));
            }
        }
    }

    private static bool TryResolveAppearance(
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, ResolvedAppearance> colorsByIfcGuid,
        out ResolvedAppearance appearance)
    {
        appearance = default;
        if (args.Count > 0)
        {
            string globalId = Unquote(args[0]);
            if (globalId.Length == 22 && colorsByIfcGuid.TryGetValue(globalId, out appearance))
                return true;
        }

        foreach (string arg in args)
        {
            string value = Unquote(arg);
            if (!LooksLikeUniqueId(value) && value.Length != 22)
                continue;

            if (colorsByIfcGuid.TryGetValue(value, out appearance))
                return true;
        }

        return false;
    }

    private static bool LooksLikeUniqueId(string value) =>
        value.Length >= 36 && value.Contains('-', StringComparison.Ordinal);

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

    private static bool IsLineAlreadyRewritten(int lineIndex, Dictionary<int, string> replacements) =>
        replacements.ContainsKey(lineIndex);

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

    private readonly record struct ProductWork(
        Entity Product,
        ResolvedAppearance Appearance,
        int RepresentationId,
        int TypeId,
        IReadOnlyList<string> Args);
}
