using Autodesk.Revit.DB;
using MetroCIM.Models;

namespace MetroCIM.Models;

public sealed class ElementGeometry
{
    public required string Id { get; init; }
    public required Element Element { get; init; }
    public required ResolvedAppearance Appearance { get; init; }
    public required TriangleMesh Mesh { get; init; }
}
