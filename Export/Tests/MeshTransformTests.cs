using MetroCIM.Models;
using MetroCIM.Services;
using System.Numerics;
using Xunit;

namespace MetroCIM.Tests;

public sealed class MeshTransformTests
{
    [Fact]
    public void ApplyTranslatesPositionsAndKeepsTriangleWinding()
    {
        var mesh = new TriangleMesh();
        mesh.AddTriangle(
            new Vector3(1, 0, 0),
            new Vector3(2, 0, 0),
            new Vector3(1, 1, 0));

        Matrix4x4 matrix = Matrix4x4.CreateTranslation(-1, 0, 0);
        MeshTransform.Apply(mesh, matrix);

        Assert.Equal(new Vector3(0, 0, 0), mesh.Positions[0]);
        Assert.Equal(new Vector3(1, 0, 0), mesh.Positions[1]);
        Assert.Equal(new Vector3(0, 1, 0), mesh.Positions[2]);
        Assert.Equal(3, mesh.Indices.Count);
    }

    [Fact]
    public void CreateLocalFromOriginMovesOriginToZero()
    {
        Matrix4x4 matrix = MeshTransform.CreateLocalFromOrigin(10, 20, 30, 0);
        Vector3 moved = Vector3.Transform(new Vector3(10, 20, 30), matrix);
        Assert.True(moved.Length() < 1e-5f);
    }

    [Fact]
    public void CoordinateModePreferenceRoundTrips()
    {
        string dir = Path.Combine(Path.GetTempPath(), "MetroCIM-coord-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            CoordinateModePreference.Write(dir, CoordinateMode.Shared);
            Assert.Equal(CoordinateMode.Shared, CoordinateModePreference.Read(dir));
            Assert.Equal(CoordinateMode.Shared, CoordinateModePreference.TryParse("SharedCoordination"));
            Assert.Equal(CoordinateMode.Local, CoordinateModePreference.TryParse("LocalLocation"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
