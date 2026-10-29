using MetroCIM.Models;

namespace MetroCIM.Services;

/// <summary>
/// IFC display color: view filter, then graphic override, then MEP system type / color fill, then the element's own color.
/// </summary>
public static class IfcColorPriority
{
    public static ResolvedAppearance Select(
        ResolvedAppearance? viewFilter,
        ResolvedAppearance? graphicOverride,
        ResolvedAppearance? mepSystemOrColorFill,
        ResolvedAppearance? elementMaterial)
    {
        if (viewFilter is not null)
            return viewFilter.Value;

        if (graphicOverride is not null)
            return graphicOverride.Value;

        if (mepSystemOrColorFill is not null)
            return mepSystemOrColorFill.Value;

        if (elementMaterial is not null)
            return elementMaterial.Value;

        return ResolvedAppearance.Default;
    }
}
