namespace MetroCIM.Models;

/// <summary>
/// Export origin for mesh writers (glTF / XKT / Navisworks IFC).
/// Revit IFC uses the selected Revit IFC setup instead.
/// </summary>
public enum CoordinateMode
{
    /// <summary>Revit: Project Base Point. Navisworks: primary model local origin.</summary>
    Local = 0,

    /// <summary>Revit: Shared Coordinates. Navisworks: federated world coordinates.</summary>
    Shared = 1
}
