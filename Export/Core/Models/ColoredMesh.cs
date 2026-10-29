namespace MetroCIM.Models;

/// <summary>Host-free colored mesh used by glTF / IFC writers.</summary>
public sealed class ColoredMesh
{
    public ColoredMesh(string id, ResolvedAppearance appearance, TriangleMesh mesh)
    {
        Id = id;
        Appearance = appearance;
        Mesh = mesh;
    }

    public string Id { get; }
    public ResolvedAppearance Appearance { get; }
    public TriangleMesh Mesh { get; }
}
