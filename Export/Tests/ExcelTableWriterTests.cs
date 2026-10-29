using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MetroCIM.Models;
using MetroCIM.Services;
using Xunit;

namespace MetroCIM.Tests;

public sealed class ExcelTableWriterTests
{
    [Fact]
    public void WriteCreatesWorkbookWithIdentityAndParameterColumns()
    {
        var table = new ParameterTable();
        var row = new ParameterTableRow
        {
            RevitId = "123",
            IfcGuid = "2AbcDefGhIjKlMnOpQrStU",
            UniqueId = "unique-1",
            Category = "Pipes",
            Family = "Pipe Types",
            TypeName = "Standard",
            Name = "Pipe 1"
        };
        row.Parameters["Instance.Mark"] = "P-1";
        row.Parameters["Type.Diameter"] = "100 mm";
        table.Rows.Add(row);

        string path = Path.Combine(Path.GetTempPath(), "MetroCIM-excel-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            ExcelTableWriter.Write(path, table);
            Assert.True(File.Exists(path));

            using SpreadsheetDocument document = SpreadsheetDocument.Open(path, false);
            WorksheetPart worksheet = document.WorkbookPart!.WorksheetParts.First();
            SheetData data = worksheet.Worksheet.GetFirstChild<SheetData>()!;
            List<Row> rows = data.Elements<Row>().ToList();
            Assert.Equal(2, rows.Count);

            List<string> headers = rows[0].Elements<Cell>().Select(ReadCell).ToList();
            Assert.Contains("RevitId", headers);
            Assert.Contains("IfcGuid", headers);
            Assert.Contains("Instance.Mark", headers);
            Assert.Contains("Type.Diameter", headers);

            List<string> values = rows[1].Elements<Cell>().Select(ReadCell).ToList();
            Assert.Contains("123", values);
            Assert.Contains("2AbcDefGhIjKlMnOpQrStU", values);
            Assert.Contains("P-1", values);
            Assert.Contains("100 mm", values);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static string ReadCell(Cell cell)
    {
        if (cell.InlineString?.Text is not null)
            return cell.InlineString.Text.Text ?? string.Empty;
        return cell.CellValue?.Text ?? string.Empty;
    }
}
