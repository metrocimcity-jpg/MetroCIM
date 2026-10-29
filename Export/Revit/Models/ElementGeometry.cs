using Autodesk.Revit.DB;
using MetroCIM.Models;

namespace MetroCIM.Models;

public sealed class ElementGeometry
{
    public ElementGeometry(string id, Element element, ResolvedAppearance appearance, TriangleMesh mesh)
    {
        Id = id;
        Element = element;
        Appearance = appearance;
        Mesh = mesh;
    }

    public string Id { get; }
    public Element Element { get; }
    public ResolvedAppearance Appearance { get; }
    public TriangleMesh Mesh { get; }
}
