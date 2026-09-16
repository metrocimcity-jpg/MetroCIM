using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using MetroCIM.Models;
using System.Numerics;
using System.Runtime.InteropServices;

namespace MetroCIM.Navisworks.Services;

public sealed class GeometryBuilder
{
    public TriangleMesh? BuildMesh(Document document, ModelItem item)
    {
        try
        {
            InwOaPath? path = ComApiBridge.ToInwOaPath(item);
            if (path is null)
                return null;

            double toMeters = UnitConversion.ScaleFactor(document.Units, Units.Meters);
            var listener = new TriangleCallback(toMeters);
            const nwEVertexProperty props =
                nwEVertexProperty.eNORMAL | nwEVertexProperty.eCOLOR;

            foreach (InwOaFragment3 fragment in path.Fragments())
                fragment.GenerateSimplePrimitives(props, listener);

            return listener.Mesh.IsEmpty ? null : listener.Mesh;
        }
        catch
        {
            return null;
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class TriangleCallback : InwSimplePrimitivesCB
    {
        private readonly double _toMeters;
        public TriangleMesh Mesh { get; } = new();

        public TriangleCallback(double toMeters) => _toMeters = toMeters;

        public void Triangle(InwSimpleVertex v1, InwSimpleVertex v2, InwSimpleVertex v3)
        {
            Mesh.AddTriangle(ToYUp(v1), ToYUp(v2), ToYUp(v3));
        }

        public void Line(InwSimpleVertex v1, InwSimpleVertex v2)
        {
        }

        public void Point(InwSimpleVertex v1)
        {
        }

        public void SnapPoint(InwSimpleVertex v1)
        {
        }

        private Vector3 ToYUp(InwSimpleVertex vertex)
        {
            Array coords = (Array)vertex.coord;
            float x = Convert.ToSingle(coords.GetValue(0)) * (float)_toMeters;
            float y = Convert.ToSingle(coords.GetValue(1)) * (float)_toMeters;
            float z = Convert.ToSingle(coords.GetValue(2)) * (float)_toMeters;
            // Navisworks Z-up → glTF Y-up
            return new Vector3(x, z, -y);
        }
    }
}
