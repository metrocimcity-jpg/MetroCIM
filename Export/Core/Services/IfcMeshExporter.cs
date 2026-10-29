using System.Globalization;
using System.Text;
using MetroCIM.Models;

namespace MetroCIM.Services;

/// <summary>
/// Writes IFC4 from colored triangle meshes (used by Navisworks Export IFC).
/// </summary>
public static class IfcMeshExporter
{
    public static void Export(
        string path,
        IReadOnlyList<ColoredMesh> items,
        string? projectName = null)
    {
        if (items.Count == 0 || items.All(item => item.Mesh.IsEmpty))
            throw new InvalidOperationException("No triangulated geometry was produced for IFC export.");

        var batches = new Dictionary<ResolvedAppearance, TriangleMesh>();
        foreach (ColoredMesh item in items)
        {
            if (item.Mesh.IsEmpty)
                continue;

            if (!batches.TryGetValue(item.Appearance, out TriangleMesh? mesh))
            {
                mesh = new TriangleMesh();
                batches[item.Appearance] = mesh;
            }

            mesh.Append(item.Mesh);
        }

        ExportBatched(path, batches, projectName);
    }

    public static void ExportBatched(
        string path,
        IReadOnlyDictionary<ResolvedAppearance, TriangleMesh> meshesByColor,
        string? projectName = null)
    {
        var nonEmpty = meshesByColor
            .Where(pair => !pair.Value.IsEmpty)
            .ToList();
        if (nonEmpty.Count == 0)
            throw new InvalidOperationException("No triangulated geometry was produced for IFC export.");

        string name = string.IsNullOrWhiteSpace(projectName) ? "MetroCIM" : Sanitize(projectName!);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, BuildDocument(path, name, nonEmpty), Encoding.ASCII);
    }

    private static string BuildDocument(
        string path,
        string projectName,
        List<KeyValuePair<ResolvedAppearance, TriangleMesh>> batches)
    {
        var sb = new StringBuilder(capacity: 512 * 1024);
        int id = 1;
        int Next() => id++;

        sb.AppendLine("ISO-10303-21;");
        sb.AppendLine("HEADER;");
        sb.AppendLine("FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'),'2;1');");
        sb.AppendLine(
            $"FILE_NAME('{Escape(Path.GetFileName(path))}','{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}',('MetroCIM'),('MetroCIM'),'MetroCIM','MetroCIM','');");
        sb.AppendLine("FILE_SCHEMA(('IFC4'));");
        sb.AppendLine("ENDSEC;");
        sb.AppendLine("DATA;");

        int person = Next();
        sb.AppendLine($"#{person}=IFCPERSON($,$,'MetroCIM',$,$,$,$,$);");
        int org = Next();
        sb.AppendLine($"#{org}=IFCORGANIZATION($,'MetroCIM',$,$,$);");
        int personOrg = Next();
        sb.AppendLine($"#{personOrg}=IFCPERSONANDORGANIZATION(#{person},#{org},$);");
        int app = Next();
        sb.AppendLine($"#{app}=IFCAPPLICATION(#{org},'1.0','MetroCIM','MetroCIM');");
        int owner = Next();
        sb.AppendLine($"#{owner}=IFCOWNERHISTORY(#{personOrg},#{app},$,.ADDED.,$,$,$,0);");

        int siLength = Next();
        sb.AppendLine($"#{siLength}=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);");
        int units = Next();
        sb.AppendLine($"#{units}=IFCUNITASSIGNMENT((#{siLength}));");

        int origin = Next();
        sb.AppendLine($"#{origin}=IFCCARTESIANPOINT((0.,0.,0.));");
        int axis = Next();
        sb.AppendLine($"#{axis}=IFCAXIS2PLACEMENT3D(#{origin},$,$);");
        int context = Next();
        sb.AppendLine($"#{context}=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.0E-05,#{axis},$);");
        int bodyContext = Next();
        sb.AppendLine(
            $"#{bodyContext}=IFCGEOMETRICREPRESENTATIONSUBCONTEXT('Body','Model',*,*,*,*,#{context},$,.MODEL_VIEW.,$);");

        int project = Next();
        sb.AppendLine(
            $"#{project}=IFCPROJECT('{IfcGuid.NewIfcGlobalId()}',#{owner},'{Escape(projectName)}',$,$,$,$,(#{context}),#{units});");

        int localOrigin = Next();
        sb.AppendLine($"#{localOrigin}=IFCCARTESIANPOINT((0.,0.,0.));");
        int localAxis = Next();
        sb.AppendLine($"#{localAxis}=IFCAXIS2PLACEMENT3D(#{localOrigin},$,$);");
        int localPlacement = Next();
        sb.AppendLine($"#{localPlacement}=IFCLOCALPLACEMENT($,#{localAxis});");

        int site = Next();
        sb.AppendLine(
            $"#{site}=IFCSITE('{IfcGuid.NewIfcGlobalId()}',#{owner},'Site',$,$,#{localPlacement},$,$,.ELEMENT.,$,$,$,$,$);");
        int building = Next();
        sb.AppendLine(
            $"#{building}=IFCBUILDING('{IfcGuid.NewIfcGlobalId()}',#{owner},'Building',$,$,#{localPlacement},$,$,.ELEMENT.,$,$,$);");
        int storey = Next();
        sb.AppendLine(
            $"#{storey}=IFCBUILDINGSTOREY('{IfcGuid.NewIfcGlobalId()}',#{owner},'Level 0',$,$,#{localPlacement},$,$,.ELEMENT.,0.);");

        int relSite = Next();
        sb.AppendLine(
            $"#{relSite}=IFCRELAGGREGATES('{IfcGuid.NewIfcGlobalId()}',#{owner},$,$,#{project},(#{site}));");
        int relBuilding = Next();
        sb.AppendLine(
            $"#{relBuilding}=IFCRELAGGREGATES('{IfcGuid.NewIfcGlobalId()}',#{owner},$,$,#{site},(#{building}));");
        int relStorey = Next();
        sb.AppendLine(
            $"#{relStorey}=IFCRELAGGREGATES('{IfcGuid.NewIfcGlobalId()}',#{owner},$,$,#{building},(#{storey}));");

        var productIds = new List<int>();
        int batchIndex = 0;
        foreach (KeyValuePair<ResolvedAppearance, TriangleMesh> batch in batches)
        {
            int productId = WriteColoredProduct(
                sb,
                ref id,
                owner,
                localPlacement,
                bodyContext,
                batch.Key,
                batch.Value,
                $"ColorBatch_{batchIndex++:D3}");
            productIds.Add(productId);
        }

        int relContained = Next();
        string contained = string.Join(",", productIds.Select(pid => "#" + pid));
        sb.AppendLine(
            $"#{relContained}=IFCRELCONTAINEDINSPATIALSTRUCTURE('{IfcGuid.NewIfcGlobalId()}',#{owner},$,$,({contained}),#{storey});");

        sb.AppendLine("ENDSEC;");
        sb.AppendLine("END-ISO-10303-21;");
        return sb.ToString();
    }

    private static int WriteColoredProduct(
        StringBuilder sb,
        ref int id,
        int owner,
        int placement,
        int bodyContext,
        ResolvedAppearance appearance,
        TriangleMesh mesh,
        string name)
    {
        var coordList = new StringBuilder();
        coordList.Append('(');
        for (int i = 0; i < mesh.Positions.Count; i++)
        {
            if (i > 0)
                coordList.Append(',');
            var p = mesh.Positions[i];
            coordList.Append('(')
                .Append(IfcReal(p.X)).Append(',')
                .Append(IfcReal(p.Y)).Append(',')
                .Append(IfcReal(p.Z)).Append(')');
        }

        coordList.Append(')');

        var faces = new StringBuilder();
        faces.Append('(');
        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            if (i > 0)
                faces.Append(',');
            faces.Append('(')
                .Append(mesh.Indices[i] + 1).Append(',')
                .Append(mesh.Indices[i + 1] + 1).Append(',')
                .Append(mesh.Indices[i + 2] + 1).Append(')');
        }

        faces.Append(')');

        int coords = ++id;
        sb.AppendLine($"#{coords}=IFCCARTESIANPOINTLIST3D({coordList},$);");
        int faceSet = ++id;
        sb.AppendLine($"#{faceSet}=IFCTRIANGULATEDFACESET(#{coords},$,.F.,{faces},$);");

        int color = ++id;
        sb.AppendLine(
            $"#{color}=IFCCOLOURRGB($,{IfcReal(appearance.R / 255d)},{IfcReal(appearance.G / 255d)},{IfcReal(appearance.B / 255d)});");
        int rendering = ++id;
        double transparency = (255 - appearance.A) / 255d;
        sb.AppendLine(
            $"#{rendering}=IFCSURFACESTYLERENDERING(#{color},{IfcReal(transparency)},$,$,$,$,$,.NOTDEFINED.);");
        int surfaceStyle = ++id;
        sb.AppendLine($"#{surfaceStyle}=IFCSURFACESTYLE('MetroCIM',.BOTH.,(#{rendering}));");
        int styledItem = ++id;
        sb.AppendLine($"#{styledItem}=IFCSTYLEDITEM(#{faceSet},(#{surfaceStyle}),$);");

        int shapeRep = ++id;
        sb.AppendLine(
            $"#{shapeRep}=IFCSHAPEREPRESENTATION(#{bodyContext},'Body','Tessellation',(#{faceSet}));");
        int pds = ++id;
        sb.AppendLine($"#{pds}=IFCPRODUCTDEFINITIONSHAPE($,$,(#{shapeRep}));");
        int product = ++id;
        sb.AppendLine(
            $"#{product}=IFCBUILDINGELEMENTPROXY('{IfcGuid.NewIfcGlobalId()}',#{owner},'{Escape(name)}',$,$,#{placement},#{pds},$,.ELEMENT.);");

        int material = ++id;
        sb.AppendLine(
            $"#{material}=IFCMATERIAL('MetroCIM {appearance.R:D3}-{appearance.G:D3}-{appearance.B:D3}-{appearance.A:D3}');");
        int relMat = ++id;
        sb.AppendLine(
            $"#{relMat}=IFCRELASSOCIATESMATERIAL('{IfcGuid.NewIfcGlobalId()}',#{owner},$,$,(#{product}),#{material});");

        return product;
    }

    private static string IfcReal(double value)
    {
        string text = value.ToString("0.######", CultureInfo.InvariantCulture);
        return text.IndexOf('.') >= 0 ? text : text + ".";
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "''");

    private static string Sanitize(string value)
    {
        var chars = value.Where(c => !char.IsControl(c)).ToArray();
        return chars.Length == 0 ? "MetroCIM" : new string(chars);
    }
}
