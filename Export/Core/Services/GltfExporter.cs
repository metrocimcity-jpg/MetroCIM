using MetroCIM.Models;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using System.Numerics;

namespace MetroCIM.Services;

public sealed class GltfExporter
{
    public void Export(string path, IReadOnlyDictionary<ResolvedAppearance, TriangleMesh> meshesByColor)
    {
        var writer = new GltfSceneAccumulator();
        foreach (KeyValuePair<ResolvedAppearance, TriangleMesh> pair in meshesByColor)
            writer.AddBatched(pair.Key, pair.Value);

        writer.Save(path);
    }

    public void ExportByElement(string path, IReadOnlyList<ColoredMesh> items)
    {
        var writer = new GltfSceneAccumulator();
        foreach (ColoredMesh item in items)
            writer.Add(item.Id, item.Appearance, item.Mesh);
        writer.Save(path);
    }
}

public sealed class GltfSceneAccumulator
{
    private const int MaxTrianglesPerBatch = 8000;
    private readonly SceneBuilder _scene = new();
    private readonly Dictionary<ResolvedAppearance, MaterialBuilder> _materials = new();
    private readonly Dictionary<ResolvedAppearance, List<BatchMesh>> _batched = new();
    private int _materialIndex;
    private int _count;

    public int Count => _count;

    public void Add(string id, ResolvedAppearance appearance, TriangleMesh mesh)
    {
        if (mesh.IsEmpty)
            return;

        var builder = new MeshBuilder<VertexPositionNormal>(id);
        AddTriangles(builder, GetMaterial(appearance), mesh);
        _scene.AddRigidMesh(builder, new NodeBuilder(id));
        _count++;
    }

    public void AddBatched(ResolvedAppearance appearance, TriangleMesh mesh)
    {
        if (mesh.IsEmpty)
            return;

        if (!_batched.TryGetValue(appearance, out List<BatchMesh>? batches))
        {
            batches = new List<BatchMesh>();
            _batched[appearance] = batches;
        }

        int incoming = mesh.Indices.Count / 3;
        if (batches.Count == 0 || batches[batches.Count - 1].Triangles + incoming > MaxTrianglesPerBatch)
        {
            string name = $"mesh_{_count:D4}";
            batches.Add(new BatchMesh(new MeshBuilder<VertexPositionNormal>(name)));
        }

        BatchMesh batch = batches[batches.Count - 1];
        AddTriangles(batch.Builder, GetMaterial(appearance), mesh);
        batch.Triangles += incoming;
        _count++;
    }

    public void Save(string path)
    {
        if (_count == 0)
            throw new InvalidOperationException("No triangulated geometry was produced for export.");

        int index = 0;
        foreach (List<BatchMesh> batches in _batched.Values)
        {
            foreach (BatchMesh batch in batches)
                _scene.AddRigidMesh(batch.Builder, new NodeBuilder($"mesh_{index++:D4}"));
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string extension = Path.GetExtension(path);
        if (extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase))
            _scene.ToGltf2().SaveGLTF(path);
        else
            _scene.ToGltf2().SaveGLB(path);
    }

    private sealed class BatchMesh
    {
        public BatchMesh(MeshBuilder<VertexPositionNormal> builder) => Builder = builder;
        public MeshBuilder<VertexPositionNormal> Builder { get; }
        public int Triangles { get; set; }
    }

    private MaterialBuilder GetMaterial(ResolvedAppearance appearance)
    {
        if (_materials.TryGetValue(appearance, out MaterialBuilder? material))
            return material;

        material = CreateMaterial(appearance, _materialIndex++);
        _materials[appearance] = material;
        return material;
    }

    private static void AddTriangles(
        MeshBuilder<VertexPositionNormal> builder,
        MaterialBuilder material,
        TriangleMesh mesh)
    {
        var prim = builder.UsePrimitive(material);
        for (int i = 0; i < mesh.Indices.Count; i += 3)
        {
            int i0 = mesh.Indices[i];
            int i1 = mesh.Indices[i + 1];
            int i2 = mesh.Indices[i + 2];
            prim.AddTriangle(
                new VertexPositionNormal(mesh.Positions[i0], mesh.Normals[i0]),
                new VertexPositionNormal(mesh.Positions[i1], mesh.Normals[i1]),
                new VertexPositionNormal(mesh.Positions[i2], mesh.Normals[i2]));
        }
    }

    private static MaterialBuilder CreateMaterial(ResolvedAppearance appearance, int index)
    {
        var material = new MaterialBuilder($"color_{index:D3}")
            .WithDoubleSide(true)
            .WithMetallicRoughnessShader()
            .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, appearance.ToBaseColor());

        if (appearance.A < 255)
            material.WithAlpha(AlphaMode.BLEND);

        return material;
    }
}
