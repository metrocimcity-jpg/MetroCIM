using MetroCIM.Services;
using Xunit;

namespace MetroCIM.Tests;

public sealed class IfcSetupNameTests
{
    [Fact]
    public void LastUsedSetupWinsWhenItStillExists()
    {
        string? selected = IfcSetupName.Resolve(
            ["IFC4 Reference View", "In-Session Setup", "Project Setup"],
            "Project Setup");

        Assert.Equal("Project Setup", selected);
    }

    [Fact]
    public void InSessionIsPreferredWhenNoLastUsedSetup()
    {
        string? selected = IfcSetupName.Resolve(
            ["IFC4 Reference View", "<In-Session Setup>"],
            lastUsed: null);

        Assert.Equal("<In-Session Setup>", selected);
    }

    [Fact]
    public void FirstSetupIsUsedWhenNothingMatches()
    {
        string? selected = IfcSetupName.Resolve(
            ["IFC2x3 Coordination View 2.0", "IFC4 Design Transfer View"],
            "Missing Setup");

        Assert.Equal("IFC2x3 Coordination View 2.0", selected);
    }
}
