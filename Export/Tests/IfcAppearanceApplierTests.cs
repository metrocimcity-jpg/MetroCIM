using MetroCIM.Models;
using MetroCIM.Services;
using Xunit;

namespace MetroCIM.Tests;

public sealed class IfcAppearanceApplierTests
{
    [Fact]
    public void MaterialNameIsStableForASystemTypeColor()
    {
        ResolvedAppearance chilled = ResolvedAppearance.FromRgb(0, 176, 240);
        Assert.Equal("MetroCIM 000-176-240-255", IfcAppearanceApplier.MaterialName(chilled));
    }

    [Fact]
    public void MaterialNameIncludesTransparency()
    {
        ResolvedAppearance glass = ResolvedAppearance.FromRgb(180, 180, 180, 40);
        Assert.Equal("MetroCIM 180-180-180-153", IfcAppearanceApplier.MaterialName(glass));
    }

    [Fact]
    public void TemporaryExportMaterialsAreNotUsedAsSystemTypeColor()
    {
        Assert.True(IfcAppearanceApplier.IsTemporaryMaterialName("MetroCIM 255-000-000-255"));
        Assert.False(IfcAppearanceApplier.IsTemporaryMaterialName("Supply Air"));
        Assert.False(IfcAppearanceApplier.IsTemporaryMaterialName(null));
    }

    [Fact]
    public void EmptyGuidEncodesToTwentyTwoZeros()
    {
        Assert.Equal("0000000000000000000000", IfcGuid.From(Guid.Empty));
    }
}
