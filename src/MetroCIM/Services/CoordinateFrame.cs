using Autodesk.Revit.DB;
using MetroCIM.Models;

namespace MetroCIM.Services;

internal static class CoordinateFrame
{
    /// <summary>
    /// Transform applied to Internal coordinates so the chosen origin sits at (0,0,0).
    /// </summary>
    public static Transform Resolve(Document document, CoordinateMode mode)
    {
        return mode == CoordinateMode.Shared
            ? SharedToExport(document)
            : ProjectBasePointToExport(document);
    }

    private static Transform ProjectBasePointToExport(Document document)
    {
        BasePoint? basePoint = BasePoint.GetProjectBasePoint(document);
        if (basePoint is null)
            return Transform.Identity;

        XYZ origin = basePoint.Position;
        double angle = 0;
        Parameter? angleParam = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_ANGLETON_PARAM);
        if (angleParam is not null && angleParam.HasValue)
            angle = angleParam.AsDouble();

        Transform translate = Transform.CreateTranslation(-origin);
        Transform rotate = Transform.CreateRotation(XYZ.BasisZ, -angle);
        return rotate.Multiply(translate);
    }

    private static Transform SharedToExport(Document document)
    {
        try
        {
            ProjectLocation? location = document.ActiveProjectLocation;
            if (location is null)
                return Transform.Identity;

            // GetTotalTransform: shared → internal. Inverse: internal → shared.
            return location.GetTotalTransform().Inverse;
        }
        catch (Autodesk.Revit.Exceptions.ApplicationException)
        {
            return Transform.Identity;
        }
    }
}
