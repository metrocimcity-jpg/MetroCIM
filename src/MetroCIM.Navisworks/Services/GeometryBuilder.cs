using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using MetroCIM.Models;
using System.Numerics;
using System.Runtime.InteropServices;

namespace MetroCIM.Navisworks.Services;

public sealed class GeometryBuilder
{
    public TriangleMesh? BuildMesh(
        Document document,
        ModelItem item,
        CoordinateMode coordinateMode = CoordinateMode.Shared)
    {
        try
        {
            InwOaPath? path = ComApiBridge.ToInwOaPath(item);
            if (path is null)
                return null;

            double toMeters = UnitConversion.ScaleFactor(document.Units, Units.Meters);
            Vector3D? localOrigin = ResolvePrimaryModelOrigin(document, coordinateMode);
            var listener = new TriangleCallback(toMeters, localOrigin);
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

    /// <summary>
    /// Local: subtract primary model transform translation so that model's origin is at export (0,0,0).
    /// Shared: leave world/federated coordinates unchanged.
    /// </summary>
    private static Vector3D? ResolvePrimaryModelOrigin(Document document, CoordinateMode mode)
    {
        if (mode != CoordinateMode.Local)
            return null;

        try
        {
            var models = document.Models;
            if (models is null || models.Count == 0)
                return null;

            Transform3D? worldFromLocal = models[0].Transform;
            return worldFromLocal?.Translation;
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
        private readonly Vector3D? _localOrigin;
        public TriangleMesh Mesh { get; } = new();

        public TriangleCallback(double toMeters, Vector3D? localOrigin)
        {
            _toMeters = toMeters;
            _localOrigin = localOrigin;
        }

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
            double x = Convert.ToDouble(coords.GetValue(0));
            double y = Convert.ToDouble(coords.GetValue(1));
            double z = Convert.ToDouble(coords.GetValue(2));

            if (_localOrigin is Vector3D origin)
            {
                x -= origin.X;
                y -= origin.Y;
                z -= origin.Z;
            }

            float sx = (float)(x * _toMeters);
            float sy = (float)(y * _toMeters);
            float sz = (float)(z * _toMeters);
            // Navisworks Z-up → glTF Y-up
            return new Vector3(sx, sz, -sy);
        }
    }
}
