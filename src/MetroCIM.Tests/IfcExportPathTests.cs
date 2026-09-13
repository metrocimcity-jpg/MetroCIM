using MetroCIM.Services;
using Xunit;

namespace MetroCIM.Tests;

public sealed class IfcExportPathTests
{
    [Theory]
    [InlineData(@"C:\out\Boiler Room.ifc", "Boiler Room")]
    [InlineData(@"C:\out\model.ifcXML", "model")]
    [InlineData(@"C:\out\export", "export")]
    public void ExportNameStripsTheExtension(string path, string expected)
    {
        Assert.Equal(expected, IfcExportPath.ExportName(path));
    }

    [Theory]
    [InlineData(@"C:\out\model.ifc", "0")]
    [InlineData(@"C:\out\model.IFC", "0")]
    [InlineData(@"C:\out\model.ifcXML", "1")]
    [InlineData(@"C:\out\model.ifcZIP", "2")]
    public void FileTypeOptionMatchesTheChosenExtension(string path, string expected)
    {
        Assert.Equal(expected, IfcExportPath.FileTypeOption(path));
    }

    [Fact]
    public void FindWrittenFilePrefersTheRequestedPathWhenItHasContent()
    {
        string folder = Path.Combine(Path.GetTempPath(), "MetroCIM-IfcExportPath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string requested = Path.Combine(folder, "view.ifc");
            File.WriteAllText(requested, "ISO-10303-21;");

            Assert.Equal(requested, IfcExportPath.FindWrittenFile(requested));
            Assert.False(IfcExportPath.IsEmpty(requested));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void FindWrittenFileUsesTheSiblingWhenTheChosenPathIsEmpty()
    {
        string folder = Path.Combine(Path.GetTempPath(), "MetroCIM-IfcExportPath-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string requested = Path.Combine(folder, "view.ifc");
            File.WriteAllText(requested, string.Empty);
            string sibling = Path.Combine(folder, "view.ifc.ifc");
            File.WriteAllText(sibling, "ISO-10303-21;");

            Assert.Equal(sibling, IfcExportPath.FindWrittenFile(requested));
            Assert.True(IfcExportPath.IsEmpty(requested));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
