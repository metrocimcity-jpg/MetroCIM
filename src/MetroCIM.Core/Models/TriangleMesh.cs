using System.Numerics;

namespace MetroCIM.Models;

public sealed class TriangleMesh
{
    public List<Vector3> Positions { get; } = new();
    public List<Vector3> Normals { get; } = new();
    public List<int> Indices { get; } = new();

    public bool IsEmpty => Indices.Count < 3;

    public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (n.LengthSquared() < 1e-20f)
            return;

        n = Vector3.Normalize(n);
        int i = Positions.Count;
        Positions.Add(a);
        Positions.Add(b);
        Positions.Add(c);
        Normals.Add(n);
        Normals.Add(n);
        Normals.Add(n);
        Indices.Add(i);
        Indices.Add(i + 1);
        Indices.Add(i + 2);
    }

    public void Append(TriangleMesh other)
    {
        int offset = Positions.Count;
        Positions.AddRange(other.Positions);
        Normals.AddRange(other.Normals);
        foreach (int index in other.Indices)
            Indices.Add(index + offset);
    }
}
