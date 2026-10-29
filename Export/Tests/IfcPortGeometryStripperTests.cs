using MetroCIM.Services;
using Xunit;

namespace MetroCIM.Tests;

public sealed class IfcPortGeometryStripperTests
{
    [Fact]
    public void PatchTextDeletesDistributionPortsEvenWithoutRepresentation()
    {
        const string ifc =
            "ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n" +
            "#9=IFCFLOWSEGMENT('0k2Hq$n6f8VhP8nZQlY$xK',#1,'Pipe',$,$,#11,#12,'1');\n" +
            "#10=IFCDISTRIBUTIONPORT('1k2Hq$n6f8VhP8nZQlY$xK',#1,'OutPort_16465958_2','Flow',$,#13,$,.SOURCE.,$,$);\n" +
            "#14=IFCRELNESTS('2k2Hq$n6f8VhP8nZQlY$xK',#1,$,$,#9,(#10));\n" +
            "#15=IFCRELCONNECTSPORTTOELEMENT('3k2Hq$n6f8VhP8nZQlY$xK',#1,'9|guid','Flow',#10,#9);\n" +
            "ENDSEC;\nEND-ISO-10303-21;\n";

        string patched = IfcPortGeometryStripper.PatchText(ifc);

        Assert.Contains("IFCFLOWSEGMENT", patched);
        Assert.DoesNotContain("IFCDISTRIBUTIONPORT", patched);
        Assert.DoesNotContain("OutPort_16465958_2", patched);
        Assert.DoesNotContain("IFCRELNESTS", patched);
        Assert.DoesNotContain("IFCRELCONNECTSPORTTOELEMENT", patched);
    }

    [Fact]
    public void PatchTextKeepsNonPortNestedObjects()
    {
        const string ifc =
            "ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n" +
            "#9=IFCFLOWSEGMENT('0k2Hq$n6f8VhP8nZQlY$xK',#1,'Pipe',$,$,#11,#12,'1');\n" +
            "#10=IFCDISTRIBUTIONPORT('1k2Hq$n6f8VhP8nZQlY$xK',#1,'InPort_1','Flow',$,#13,$,.SINK.,$,$);\n" +
            "#16=IFCFLOWFITTING('2k2Hq$n6f8VhP8nZQlY$xK',#1,'Elbow',$,$,#17,#18,'55',.NOTDEFINED.);\n" +
            "#14=IFCRELNESTS('3k2Hq$n6f8VhP8nZQlY$xK',#1,$,$,#9,(#10,#16));\n" +
            "ENDSEC;\nEND-ISO-10303-21;\n";

        string patched = IfcPortGeometryStripper.PatchText(ifc);

        Assert.DoesNotContain("IFCDISTRIBUTIONPORT", patched);
        Assert.Contains("#14=IFCRELNESTS('3k2Hq$n6f8VhP8nZQlY$xK',#1,$,$,#9,(#16));", patched);
        Assert.Contains("IFCFLOWFITTING", patched);
    }

    [Fact]
    public void PatchTextLeavesPipesUnchangedWhenThereAreNoPorts()
    {
        const string ifc =
            "ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n" +
            "#10=IFCFLOWSEGMENT('0k2Hq$n6f8VhP8nZQlY$xK',#1,'Pipe',$,$,#11,#12,'1');\n" +
            "ENDSEC;\nEND-ISO-10303-21;\n";

        string patched = IfcPortGeometryStripper.PatchText(ifc);

        Assert.Equal(ifc, patched);
    }
}
