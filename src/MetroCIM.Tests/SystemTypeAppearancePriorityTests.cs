using MetroCIM.Models;
using MetroCIM.Services;
using Xunit;

namespace MetroCIM.Tests;

public sealed class SystemTypeAppearancePriorityTests
{
    private static readonly ResolvedAppearance PipeMaterialBlack = ResolvedAppearance.FromRgb(12, 12, 12);
    private static readonly ResolvedAppearance SystemMaterialPink = ResolvedAppearance.FromRgb(200, 40, 90);
    private static readonly ResolvedAppearance SystemLineGold = ResolvedAppearance.FromRgb(214, 176, 77);
    private static readonly ResolvedAppearance SystemFillWhite = ResolvedAppearance.FromRgb(245, 245, 245);

    [Fact]
    public void SystemTypeMaterialWins_PipeMaterialIsNotAnInput()
    {
        ResolvedAppearance? selected = SystemTypeAppearancePriority.Select(
            systemTypeMaterial: SystemMaterialPink,
            systemTypeFill: SystemFillWhite,
            systemTypeLine: SystemLineGold);

        Assert.Equal(SystemMaterialPink, selected);

        // Pipe segment / pipe type material cannot participate in this function.
        _ = PipeMaterialBlack;
    }

    [Fact]
    public void BlackSystemTypeMaterialFallsThroughToSystemLineColor()
    {
        ResolvedAppearance? selected = SystemTypeAppearancePriority.Select(
            systemTypeMaterial: ResolvedAppearance.FromRgb(0, 0, 0),
            systemTypeFill: null,
            systemTypeLine: SystemLineGold);

        Assert.Equal(SystemLineGold, selected);
    }

    [Fact]
    public void SystemFillIsUsedWhenMaterialIsMissing()
    {
        ResolvedAppearance? selected = SystemTypeAppearancePriority.Select(
            systemTypeMaterial: null,
            systemTypeFill: SystemFillWhite,
            systemTypeLine: SystemLineGold);

        Assert.Equal(SystemFillWhite, selected);
    }

    [Fact]
    public void NearBlackFillIsSkippedInFavorOfLineColor()
    {
        ResolvedAppearance? selected = SystemTypeAppearancePriority.Select(
            systemTypeMaterial: null,
            systemTypeFill: ResolvedAppearance.FromRgb(8, 8, 8),
            systemTypeLine: SystemLineGold);

        Assert.Equal(SystemLineGold, selected);
    }

    [Fact]
    public void ReturnsNullWhenSystemTypeHasNoGraphics()
    {
        ResolvedAppearance? selected = SystemTypeAppearancePriority.Select(null, null, null);
        Assert.Null(selected);
    }

    [Fact]
    public void HasUsableColorRejectsPipeLikeBlack()
    {
        Assert.False(SystemTypeAppearancePriority.HasUsableColor(PipeMaterialBlack));
        Assert.True(SystemTypeAppearancePriority.HasUsableColor(SystemMaterialPink));
    }
}

public sealed class ResolvedAppearanceTests
{
    [Fact]
    public void FromRgbMapsTransparencyToAlpha()
    {
        ResolvedAppearance solid = ResolvedAppearance.FromRgb(10, 20, 30);
        Assert.Equal(255, solid.A);

        ResolvedAppearance half = ResolvedAppearance.FromRgb(10, 20, 30, 50);
        Assert.Equal(128, half.A);
    }

    [Fact]
    public void DefaultIsNeutralGrayNotPipeBlack()
    {
        Assert.False(ResolvedAppearance.Default.IsNearBlack);
        Assert.Equal(180, ResolvedAppearance.Default.R);
    }
}
