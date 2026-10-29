using MetroCIM.Models;
using MetroCIM.Services;
using System.Numerics;
using Xunit;

namespace MetroCIM.Tests;

public sealed class IfcMeshExporterTests
{
    [Fact]
    public void ExportBatchedWritesIfc4TriangulatedGeometryAndColor()
    {
        var mesh = new TriangleMesh();
        mesh.AddTriangle(
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0));

        string path = Path.Combine(Path.GetTempPath(), "MetroCIM-ifc-mesh-" + Guid.NewGuid().ToString("N") + ".ifc");
        try
        {
            IfcMeshExporter.ExportBatched(
                path,
                new Dictionary<ResolvedAppearance, TriangleMesh>
                {
                    [ResolvedAppearance.FromRgb(0, 176, 240)] = mesh
                },
                "TestProject");

            string text = File.ReadAllText(path);
            Assert.Contains("FILE_SCHEMA(('IFC4'))", text);
            Assert.Contains("IFCTRIANGULATEDFACESET", text);
            Assert.Contains("IFCCOLOURRGB($,0.,0.690196,0.941176)", text);
            Assert.Contains("IFCBUILDINGELEMENTPROXY", text);
            Assert.Contains("MetroCIM 000-176-240-255", text);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
