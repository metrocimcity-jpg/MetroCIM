using RevitXKT.Models;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using System.Numerics;

namespace RevitXKT.Services;

public sealed class GltfExporter
{
    public void Export(string path, IReadOnlyDictionary<ResolvedAppearance, TriangleMesh> meshesByColor)
    {
        if (meshesByColor.Count == 0)
            throw new InvalidOperationException("No triangulated geometry was produced for the active view.");

        var scene = new SceneBuilder();
        int index = 0;

        foreach ((ResolvedAppearance appearance, TriangleMesh mesh) in meshesByColor)
        {
            if (mesh.IsEmpty)
                continue;

            MaterialBuilder material = CreateMaterial(appearance, index);
            var builder = new MeshBuilder<VertexPositionNormal>($"mesh_{index:D3}");
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

            scene.AddRigidMesh(builder, Matrix4x4.Identity);
            index++;
        }

        string extension = Path.GetExtension(path);
        if (extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase))
            scene.ToGltf2().SaveGLTF(path);
        else
            scene.ToGltf2().SaveGLB(path);
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
