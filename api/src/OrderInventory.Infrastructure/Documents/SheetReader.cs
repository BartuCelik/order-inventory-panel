using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ClosedXML.Excel;
using OrderInventory.Application.Common;

namespace OrderInventory.Infrastructure.Documents;

/// <summary>
/// Reads the first sheet of an .xlsx upload as text: the first non-empty row holds the column names, every
/// non-empty row below it is a row. Numbers are written invariantly ("12.5") whatever the user's Excel
/// language, so the services parse one format.
/// </summary>
public static class SheetReader
{
    /// <summary>Upload limit for an import. An .xlsx is zipped, so 10,000 rows are well under 1 MB.</summary>
    public const long MaxBytes = 5 * 1024 * 1024;

    public static ImportSheet Read(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception e) when (e is InvalidDataException or FileFormatException or ArgumentException or InvalidOperationException
            or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            throw new ValidationException("The file is not an Excel workbook (.xlsx). Save it as .xlsx and try again.", e);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.First();
            var headerRow = sheet.FirstRowUsed();
            if (headerRow is null)
            {
                return new ImportSheet([], []);
            }

            var headers = headerRow.CellsUsed().ToDictionary(c => c.Address.ColumnNumber, c => Text(c).Trim());
            var repeated = headers.Values
                .Where(h => h.Length > 0)
                .GroupBy(h => h, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);
            if (repeated is not null)
            {
                throw new ImportException([new ImportError(null, $"Column '{repeated.Key}' appears more than once.")]);
            }
            var rows = sheet.RowsUsed()
                .Where(r => r.RowNumber() > headerRow.RowNumber())
                .Select(r => new SheetRow(
                    r.RowNumber(),
                    r.CellsUsed()
                        .Where(c => headers.TryGetValue(c.Address.ColumnNumber, out var name) && name.Length > 0)
                        .ToDictionary(c => headers[c.Address.ColumnNumber], Text, StringComparer.OrdinalIgnoreCase)))
                .Where(r => r.Cells.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                .ToList();

            return new ImportSheet(headers.Values.Where(h => h.Length > 0).ToList(), rows);
        }
    }

    private static string Text(IXLCell cell)
    {
        var value = cell.Value;
        return value.Type switch
        {
            XLDataType.Blank => "",
            XLDataType.Text => value.GetText(),
            XLDataType.Boolean => value.GetBoolean() ? "true" : "false",
            // Converting through decimal keeps what Excel shows (0.1, not 0.1000000000000000055…).
            // Beyond decimal's range the text fails to parse later and is reported as "not a number".
            XLDataType.Number => Math.Abs(value.GetNumber()) < 1e15
                ? ((decimal)value.GetNumber()).ToString(CultureInfo.InvariantCulture)
                : value.GetNumber().ToString(CultureInfo.InvariantCulture),
            _ => cell.GetFormattedString(),
        };
    }
}
