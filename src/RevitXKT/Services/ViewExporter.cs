using Autodesk.Revit.DB;
using RevitXKT.Models;

namespace RevitXKT.Services;

public sealed class ViewExporter
{
    private readonly VisibilityService _visibility = new();
    private readonly ColorResolver _colors = new();
    private readonly GeometryBuilder _geometry = new();
    private readonly GltfExporter _gltf = new();

    public Dictionary<ResolvedAppearance, TriangleMesh>? BuildMeshes(Document document, View3D view, out int elementCount)
    {
        var meshes = new Dictionary<ResolvedAppearance, TriangleMesh>();
        elementCount = 0;

        foreach (Element element in _visibility.GetVisibleElements(document, view))
        {
            TriangleMesh? mesh = _geometry.BuildMesh(element, view);
            if (mesh is null)
                continue;

            ResolvedAppearance appearance = _colors.Resolve(element, view);
            if (!meshes.TryGetValue(appearance, out TriangleMesh? bucket))
            {
                bucket = new TriangleMesh();
                meshes[appearance] = bucket;
            }

            bucket.Append(mesh);
            elementCount++;
        }

        return meshes.Count == 0 ? null : meshes;
    }

    public void WriteGltf(string path, IReadOnlyDictionary<ResolvedAppearance, TriangleMesh> meshes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _gltf.Export(path, meshes);
    }
}
