using MetroCIM.Models;

namespace MetroCIM.Services;

/// <summary>
/// Picks a display color from piping <em>system type</em> graphics only.
/// Pipe segment / pipe type materials are not inputs and cannot win.
/// </summary>
public static class SystemTypeAppearancePriority
{
    public static ResolvedAppearance? Select(
        ResolvedAppearance? systemTypeMaterial,
        ResolvedAppearance? systemTypeFill,
        ResolvedAppearance? systemTypeLine)
    {
        if (HasUsableColor(systemTypeMaterial))
            return systemTypeMaterial;

        if (HasUsableColor(systemTypeFill))
            return systemTypeFill;

        if (systemTypeLine is not null)
            return systemTypeLine;

        return systemTypeMaterial ?? systemTypeFill;
    }

    public static bool HasUsableColor(ResolvedAppearance? color) =>
        color is { } value && !value.IsNearBlack;
}
