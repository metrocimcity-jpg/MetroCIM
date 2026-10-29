using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using MetroCIM.Models;

namespace MetroCIM.Services;

public static class ExcelTableWriter
{
    public static void Write(string path, ParameterTable table, string sheetName = "Instances")
    {
        IReadOnlyList<string> headers = table.BuildHeaders();
        Write(path, headers, table.EnumerateCells(headers), sheetName);
    }

    public static void Write(
        string path,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows,
        string sheetName = "Instances")
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(path))
            File.Delete(path);

        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.AppendChild(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = SanitizeSheetName(sheetName)
        });

        uint rowIndex = 1;
        sheetData.AppendChild(CreateRow(rowIndex++, headers));

        foreach (IReadOnlyList<string> row in rows)
            sheetData.AppendChild(CreateRow(rowIndex++, row));

        workbookPart.Workbook.Save();
    }

    private static Row CreateRow(uint rowIndex, IReadOnlyList<string> values)
    {
        var row = new Row { RowIndex = rowIndex };
        for (int i = 0; i < values.Count; i++)
        {
            row.AppendChild(new Cell
            {
                CellReference = GetCellReference(i, rowIndex),
                DataType = CellValues.InlineString,
                InlineString = new InlineString(new Text(SanitizeCell(values[i])))
            });
        }

        return row;
    }

    private static string GetCellReference(int columnIndex, uint rowIndex)
    {
        int n = columnIndex + 1;
        var letters = new Stack<char>();
        while (n > 0)
        {
            n--;
            letters.Push((char)('A' + n % 26));
            n /= 26;
        }

        return new string(letters.ToArray()) + rowIndex;
    }

    private static string SanitizeSheetName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        name = name.Replace('[', '_').Replace(']', '_').Replace('*', '_').Replace('?', '_').Replace('/', '_').Replace('\\', '_');
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = "Instances";
        return name.Length <= 31 ? name : name.Substring(0, 31);
    }

    private static string SanitizeCell(string? value)
    {
        if (value is null || value.Length == 0)
            return string.Empty;

        // Prevent Excel formula injection when opening the workbook.
        char first = value[0];
        if (first is '=' or '+' or '-' or '@')
            return "'" + value;

        return value;
    }
}
