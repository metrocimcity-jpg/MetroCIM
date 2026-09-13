using MetroCIM.Models;
using MetroCIM.Services;
using Xunit;

namespace MetroCIM.Tests;

public sealed class IfcColorPatcherTests
{
    [Fact]
    public void SplitArgsKeepsNestedListsTogether()
    {
        IReadOnlyList<string> args = IfcColorPatcher.SplitArgs("'guid',#2,'Pipe',$,$,#11,#12,'1'");
        Assert.Equal(8, args.Count);
        Assert.Equal("'guid'", args[0]);
        Assert.Equal("#12", args[6]);
    }

    [Fact]
    public void PatchTextAddsAUniqueSurfaceStyleToThePipeBody()
    {
        const string ifc =
            "ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n" +
            "#10=IFCFLOWSEGMENT('0k2Hq$n6f8VhP8nZQlY$xK',#1,'Pipe',$,$,#11,#12,'1');\n" +
            "#12=IFCPRODUCTDEFINITIONSHAPE($,$,(#13));\n" +
            "#13=IFCSHAPEREPRESENTATION(#1,'Body','SweptSolid',(#14));\n" +
            "#14=IFCEXTRUDEDAREASOLID(#15,#16,#17,1.0);\n" +
            "ENDSEC;\nEND-ISO-10303-21;\n";

        var colors = new Dictionary<string, ResolvedAppearance>
        {
            ["0k2Hq$n6f8VhP8nZQlY$xK"] = ResolvedAppearance.FromRgb(0, 176, 240)
        };

        string patched = IfcColorPatcher.PatchText(ifc, colors);

        Assert.Contains("IFCCOLOURRGB($,0.,0.690196,0.941176)", patched);
        Assert.Contains("IFCSURFACESTYLE('MetroCIM',.BOTH.", patched);
        Assert.Contains("IFCSTYLEDITEM(#14,", patched);
        Assert.Contains("ENDSEC;", patched);
    }

    [Fact]
    public void PatchTextStylesMappedFittingGeometryAndRebindsMaterial()
    {
        const string ifc =
            "ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n" +
            "#2=IFCOWNERHISTORY(#3,#4,$,.NOCHANGE.,$,$,$,0);\n" +
            "#10=IFCFLOWFITTING('0k2Hq$n6f8VhP8nZQlY$xK',#2,'Elbow',$,$,#11,#12,'55',.NOTDEFINED.);\n" +
            "#12=IFCPRODUCTDEFINITIONSHAPE($,$,(#13));\n" +
            "#13=IFCSHAPEREPRESENTATION(#1,'Body','MappedRepresentation',(#14));\n" +
            "#14=IFCMAPPEDITEM(#30,#31);\n" +
            "#40=IFCRELASSOCIATESMATERIAL('2aaaaaaaaaaaaaaaaaaaaa',#2,$,$,(#10),#41);\n" +
            "ENDSEC;\nEND-ISO-10303-21;\n";

        var colors = new Dictionary<string, ResolvedAppearance>
        {
            ["0k2Hq$n6f8VhP8nZQlY$xK"] = ResolvedAppearance.FromRgb(0, 176, 240),
            ["55"] = ResolvedAppearance.FromRgb(0, 176, 240)
        };

        string patched = IfcColorPatcher.PatchText(ifc, colors);

        Assert.Contains("IFCSTYLEDITEM(#14,", patched);
        Assert.Contains("IFCMATERIAL('MetroCIM 000-176-240-255')", patched);
        Assert.Contains("IFCRELASSOCIATESMATERIAL('2aaaaaaaaaaaaaaaaaaaaa',#2,$,$,(),#41);", patched);
        Assert.Contains("IFCRELASSOCIATESMATERIAL(", patched);
        Assert.Contains(",(#10),#", patched);
    }

    [Fact]
    public void PatchTextReplacesAnExistingStyledItem()
    {
        const string ifc =
            "ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n" +
            "#10=IFCFLOWSEGMENT('0k2Hq$n6f8VhP8nZQlY$xK',#1,'Pipe',$,$,#11,#12,'1');\n" +
            "#12=IFCPRODUCTDEFINITIONSHAPE($,$,(#13));\n" +
            "#13=IFCSHAPEREPRESENTATION(#1,'Body','SweptSolid',(#14));\n" +
            "#14=IFCEXTRUDEDAREASOLID(#15,#16,#17,1.0);\n" +
            "#20=IFCSTYLEDITEM(#14,(#21),$);\n" +
            "ENDSEC;\nEND-ISO-10303-21;\n";

        var colors = new Dictionary<string, ResolvedAppearance>
        {
            ["0k2Hq$n6f8VhP8nZQlY$xK"] = ResolvedAppearance.FromRgb(200, 40, 90)
        };

        string patched = IfcColorPatcher.PatchText(ifc, colors);

        Assert.Contains("IFCCOLOURRGB($,0.784314,0.156863,0.352941)", patched);
        Assert.DoesNotContain("#20=IFCSTYLEDITEM(#14,(#21),$);", patched);
        Assert.Contains("#20=IFCSTYLEDITEM(#14,", patched);
    }
}
