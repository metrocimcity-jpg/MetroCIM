using Autodesk.Revit.DB;

namespace MetroCIM.Models;

public readonly record struct ExportedElement(string Id, Element Element);
