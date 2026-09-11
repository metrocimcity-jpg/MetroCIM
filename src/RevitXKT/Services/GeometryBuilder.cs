using Autodesk.Revit.DB;
using RevitXKT.Models;
using System.Numerics;

namespace RevitXKT.Services;

public sealed class GeometryBuilder
{
    private const double FeetToMeters = 0.3048;

    public TriangleMesh? BuildMesh(Element element, View3D view)
    {
        Options options = new()
        {
            ComputeReferences = false,
            IncludeNonVisibleObjects = false,
            View = view
        };

        GeometryElement? geometry;
        try
        {
            geometry = element.get_Geometry(options);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return null;
        }

        if (geometry is null)
            return null;

        var mesh = new TriangleMesh();
        Collect(geometry, Transform.Identity, mesh);
        return mesh.IsEmpty ? null : mesh;
    }

    private static void Collect(GeometryElement geometry, Transform transform, TriangleMesh mesh)
    {
        foreach (GeometryObject obj in geometry)
        {
            switch (obj)
            {
                case Solid solid:
                    AddSolid(solid, transform, mesh);
                    break;
                case Mesh revitMesh:
                    AddRevitMesh(revitMesh, transform, mesh);
                    break;
                case GeometryInstance instance:
                    GeometryElement? symbol = instance.GetSymbolGeometry();
                    if (symbol is not null)
                        Collect(symbol, transform.Multiply(instance.Transform), mesh);
                    break;
                case GeometryElement nested:
                    Collect(nested, transform, mesh);
                    break;
            }
        }
    }

    private static void AddSolid(Solid solid, Transform transform, TriangleMesh mesh)
    {
        if (solid is null || solid.Faces.IsEmpty)
            return;

        bool flipped = transform.Determinant < 0;

        foreach (Face face in solid.Faces)
        {
            Mesh? triangulated;
            try
            {
                triangulated = face.Triangulate();
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                continue;
            }

            if (triangulated is not null)
                AddRevitMesh(triangulated, transform, mesh, flipped);
        }
    }

    private static void AddRevitMesh(Mesh revitMesh, Transform transform, TriangleMesh mesh, bool flipped = false)
    {
        if (revitMesh.NumTriangles == 0)
            return;

        if (transform.Determinant < 0)
            flipped = true;

        for (int i = 0; i < revitMesh.NumTriangles; i++)
        {
            MeshTriangle triangle = revitMesh.get_Triangle(i);
            Vector3 a = ToGltf(transform.OfPoint(triangle.get_Vertex(0)));
            Vector3 b = ToGltf(transform.OfPoint(triangle.get_Vertex(1)));
            Vector3 c = ToGltf(transform.OfPoint(triangle.get_Vertex(2)));

            if (flipped)
                mesh.AddTriangle(a, c, b);
            else
                mesh.AddTriangle(a, b, c);
        }
    }

    private static Vector3 ToGltf(XYZ point)
    {
        return new Vector3(
            (float)(point.X * FeetToMeters),
            (float)(point.Z * FeetToMeters),
            (float)(-point.Y * FeetToMeters));
    }
}
