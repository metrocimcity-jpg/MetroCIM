using Autodesk.Revit.DB;
using MetroCIM.Models;
using System.Numerics;

namespace MetroCIM.Services;

public sealed class GeometryBuilder
{
    private const double FeetToMeters = 0.3048;
    private readonly Dictionary<string, List<(XYZ A, XYZ B, XYZ C)>> _symbolCache = [];

    public void ClearCache() => _symbolCache.Clear();

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

        double lod = view.DetailLevel switch
        {
            ViewDetailLevel.Coarse => 0.50,
            ViewDetailLevel.Medium => 0.60,
            _ => 0.80
        };

        var mesh = new TriangleMesh();
        Collect(geometry, Transform.Identity, mesh, lod);
        return mesh.IsEmpty ? null : mesh;
    }

    private void Collect(GeometryElement geometry, Transform transform, TriangleMesh mesh, double lod)
    {
        foreach (GeometryObject obj in geometry)
        {
            switch (obj)
            {
                case Solid solid:
                    AddSolid(solid, transform, mesh, lod);
                    break;
                case Mesh revitMesh:
                    AddRevitMesh(revitMesh, transform, mesh);
                    break;
                case GeometryInstance instance:
                    AddInstance(instance, transform, mesh, lod);
                    break;
                case GeometryElement nested:
                    Collect(nested, transform, mesh, lod);
                    break;
            }
        }
    }

    private void AddInstance(GeometryInstance instance, Transform transform, TriangleMesh mesh, double lod)
    {
        Transform world = transform.Multiply(instance.Transform);
        string? key = TrySymbolCacheKey(instance);

        if (key is not null && _symbolCache.TryGetValue(key, out List<(XYZ A, XYZ B, XYZ C)>? cached))
        {
            AppendSymbol(cached, world, mesh);
            return;
        }

        GeometryElement? symbol = instance.GetSymbolGeometry();
        if (symbol is not null)
        {
            var local = new List<(XYZ A, XYZ B, XYZ C)>();
            CollectSymbol(symbol, Transform.Identity, local, lod);
            if (local.Count > 0)
            {
                if (key is not null)
                    _symbolCache[key] = local;

                AppendSymbol(local, world, mesh);
                return;
            }
        }

        GeometryElement? instanceGeometry = instance.GetInstanceGeometry();
        if (instanceGeometry is not null)
            Collect(instanceGeometry, transform, mesh, lod);
    }

    private static string? TrySymbolCacheKey(GeometryInstance instance)
    {
        try
        {
            SymbolGeometryId geometryId = instance.GetSymbolGeometryId();
            if (geometryId is null || !geometryId.IsValidObject)
                return null;

            long symbolId = geometryId.SymbolId.Value;
            return symbolId == 0 ? null : symbolId.ToString();
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return null;
        }
    }

    private static void CollectSymbol(
        GeometryElement geometry,
        Transform transform,
        List<(XYZ A, XYZ B, XYZ C)> triangles,
        double lod)
    {
        foreach (GeometryObject obj in geometry)
        {
            switch (obj)
            {
                case Solid solid:
                    AddSolidSymbol(solid, transform, triangles, lod);
                    break;
                case Mesh revitMesh:
                    AddMeshSymbol(revitMesh, transform, triangles);
                    break;
                case GeometryInstance nestedInstance:
                    GeometryElement? nestedSymbol = nestedInstance.GetSymbolGeometry();
                    if (nestedSymbol is not null)
                    {
                        CollectSymbol(
                            nestedSymbol,
                            transform.Multiply(nestedInstance.Transform),
                            triangles,
                            lod);
                    }
                    break;
                case GeometryElement nested:
                    CollectSymbol(nested, transform, triangles, lod);
                    break;
            }
        }
    }

    private static void AddSolid(Solid solid, Transform transform, TriangleMesh mesh, double lod)
    {
        if (solid.Faces.IsEmpty)
            return;

        bool flipped = transform.Determinant < 0;
        foreach (Face face in solid.Faces)
        {
            if (face.Area < 1e-6)
                continue;

            Mesh? triangulated = Triangulate(face, lod);
            if (triangulated is not null)
                AddRevitMesh(triangulated, transform, mesh, flipped);
        }
    }

    private static void AddSolidSymbol(
        Solid solid,
        Transform transform,
        List<(XYZ A, XYZ B, XYZ C)> triangles,
        double lod)
    {
        if (solid.Faces.IsEmpty)
            return;

        foreach (Face face in solid.Faces)
        {
            if (face.Area < 1e-6)
                continue;

            Mesh? triangulated = Triangulate(face, lod);
            if (triangulated is not null)
                AddMeshSymbol(triangulated, transform, triangles);
        }
    }

    private static Mesh? Triangulate(Face face, double lod)
    {
        try
        {
            return face.Triangulate(lod);
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            try
            {
                return face.Triangulate();
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                return null;
            }
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

    private static void AddMeshSymbol(Mesh revitMesh, Transform transform, List<(XYZ A, XYZ B, XYZ C)> triangles)
    {
        for (int i = 0; i < revitMesh.NumTriangles; i++)
        {
            MeshTriangle triangle = revitMesh.get_Triangle(i);
            triangles.Add((
                transform.OfPoint(triangle.get_Vertex(0)),
                transform.OfPoint(triangle.get_Vertex(1)),
                transform.OfPoint(triangle.get_Vertex(2))));
        }
    }

    private static void AppendSymbol(
        List<(XYZ A, XYZ B, XYZ C)> triangles,
        Transform transform,
        TriangleMesh mesh)
    {
        bool flipped = transform.Determinant < 0;
        foreach ((XYZ A, XYZ B, XYZ C) tri in triangles)
        {
            Vector3 a = ToGltf(transform.OfPoint(tri.A));
            Vector3 b = ToGltf(transform.OfPoint(tri.B));
            Vector3 c = ToGltf(transform.OfPoint(tri.C));
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
