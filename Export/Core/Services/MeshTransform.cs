using MetroCIM.Models;
using System.Numerics;

namespace MetroCIM.Services;

public static class MeshTransform
{
    public static void Apply(TriangleMesh mesh, Matrix4x4 matrix)
    {
        for (int i = 0; i < mesh.Positions.Count; i++)
            mesh.Positions[i] = Vector3.Transform(mesh.Positions[i], matrix);

        if (!Matrix4x4.Invert(matrix, out Matrix4x4 inverse))
            return;

        Matrix4x4 normalMatrix = Matrix4x4.Transpose(inverse);
        for (int i = 0; i < mesh.Normals.Count; i++)
        {
            Vector3 n = Vector3.TransformNormal(mesh.Normals[i], normalMatrix);
            mesh.Normals[i] = n.LengthSquared() < 1e-20f ? mesh.Normals[i] : Vector3.Normalize(n);
        }
    }

    /// <summary>
    /// Rigid transform in right-handed XYZ (Z-up): translate by -origin, then rotate -angle about Z.
    /// </summary>
    public static Matrix4x4 CreateLocalFromOrigin(float originX, float originY, float originZ, float angleRadians)
    {
        Matrix4x4 translation = Matrix4x4.CreateTranslation(-originX, -originY, -originZ);
        Matrix4x4 rotation = Matrix4x4.CreateRotationZ(-angleRadians);
        return translation * rotation;
    }
}
