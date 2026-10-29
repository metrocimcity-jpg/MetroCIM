using MetroCIM.Models;
using MetroCIM.Services;
using Xunit;

namespace MetroCIM.Tests;

public sealed class IfcColorPriorityTests
{
    private static readonly ResolvedAppearance FilterBlue = ResolvedAppearance.FromRgb(0, 120, 215);
    private static readonly ResolvedAppearance OverrideGreen = ResolvedAppearance.FromRgb(0, 176, 80);
    private static readonly ResolvedAppearance SystemPink = ResolvedAppearance.FromRgb(200, 40, 90);
    private static readonly ResolvedAppearance MaterialGray = ResolvedAppearance.FromRgb(160, 160, 160);

    [Fact]
    public void ViewFilterWinsOverOverrideSystemAndMaterial()
    {
        ResolvedAppearance selected = IfcColorPriority.Select(FilterBlue, OverrideGreen, SystemPink, MaterialGray);
        Assert.Equal(FilterBlue, selected);
    }

    [Fact]
    public void GraphicOverrideWinsWhenThereIsNoFilter()
    {
        ResolvedAppearance selected = IfcColorPriority.Select(null, OverrideGreen, SystemPink, MaterialGray);
        Assert.Equal(OverrideGreen, selected);
    }

    [Fact]
    public void SystemOrColorFillWinsWhenFilterAndOverrideAreMissing()
    {
        ResolvedAppearance selected = IfcColorPriority.Select(null, null, SystemPink, MaterialGray);
        Assert.Equal(SystemPink, selected);
    }

    [Fact]
    public void ElementMaterialWinsWhenEarlierSourcesAreMissing()
    {
        ResolvedAppearance selected = IfcColorPriority.Select(null, null, null, MaterialGray);
        Assert.Equal(MaterialGray, selected);
    }

    [Fact]
    public void NeutralDefaultWhenNothingProvidesAColor()
    {
        ResolvedAppearance selected = IfcColorPriority.Select(null, null, null, null);
        Assert.Equal(ResolvedAppearance.Default, selected);
    }
}
