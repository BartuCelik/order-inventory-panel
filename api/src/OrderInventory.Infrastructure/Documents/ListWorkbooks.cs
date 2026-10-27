using ClosedXML.Excel;
using OrderInventory.Application.Production;
using OrderInventory.Application.Products;
using OrderInventory.Application.Sales;
using OrderInventory.Application.Stock;

namespace OrderInventory.Infrastructure.Documents;

/// <summary>
/// The list screens as Excel files: one sheet, a frozen header row with filters, and real numbers and dates
/// (not text) so the rows can be summed and sorted. English only, like the PDF.
/// </summary>
public sealed class ListWorkbooks(TimeProvider timeProvider)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string DateFormat = "yyyy-mm-dd";
    private const string TimestampFormat = "yyyy-mm-dd hh:mm";

    /// <summary><c>products-2026-10-27.xlsx</c>: the list name and today's UTC date.</summary>
    public string FileName(string list) => $"{list}-{timeProvider.GetUtcNow():yyyy-MM-dd}.xlsx";

    public static byte[] Products(IReadOnlyList<ProductListItem> rows) => Write("Products", rows,
        new("Code", p => p.Code),
        new("Name", p => p.Name),
        new("Type", p => DocumentLabels.Of(p.Type)),
        new("Unit", p => DocumentLabels.Unit(p.Unit)),
        new("On hand", p => p.OnHand),
        new("Available", p => p.Available),
        new("Minimum", p => p.MinStock),
        new("Active", p => DocumentLabels.YesNo(p.IsActive)));

    public static byte[] StockMovements(IReadOnlyList<StockMovementDto> rows) => Write("Stock movements", rows,
        new("Date (UTC)", m => m.CreatedAt, TimestampFormat),
        new("Code", m => m.ProductCode),
        new("Product", m => m.ProductName),
        new("Type", m => DocumentLabels.Of(m.Type)),
        new("Quantity", m => m.Quantity),
        new("Unit", m => DocumentLabels.Unit(m.Unit)),
        new("Note", m => m.Note),
        new("By", m => m.CreatedBy));

    public static byte[] SalesOrders(IReadOnlyList<SalesOrderSummaryDto> rows) => Write("Sales orders", rows,
        new("Number", o => o.Number),
        new("Customer", o => o.CustomerName),
        new("Order date", o => Date(o.OrderDate), DateFormat),
        new("Due date", o => Date(o.DueDate), DateFormat),
        new("Status", o => DocumentLabels.Of(o.Status)),
        new("Late", o => DocumentLabels.YesNo(o.IsLate)),
        new("Shipped (UTC)", o => o.ShippedAt, TimestampFormat));

    public static byte[] WorkOrders(IReadOnlyList<WorkOrderSummaryDto> rows) => Write("Work orders", rows,
        new("Number", w => w.Number),
        new("Code", w => w.ProductCode),
        new("Product", w => w.ProductName),
        new("Quantity", w => w.Quantity),
        new("Unit", w => DocumentLabels.Unit(w.Unit)),
        new("Status", w => DocumentLabels.Of(w.Status)),
        new("Planned date", w => Date(w.PlannedDate), DateFormat),
        new("Completed (UTC)", w => w.CompletedAt, TimestampFormat),
        new("Sales order", w => w.SalesOrderNumber),
        new("Customer", w => w.CustomerName),
        new("Material short", w => DocumentLabels.YesNo(w.HasShortage)));

    private static DateTime Date(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);

    private static byte[] Write<T>(string sheetName, IReadOnlyList<T> rows, params ReadOnlySpan<Column<T>> columns)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(sheetName);

        for (var c = 0; c < columns.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = columns[c].Header;
            for (var r = 0; r < rows.Count; r++)
            {
                sheet.Cell(r + 2, c + 1).Value = columns[c].Value(rows[r]);
            }

            if (columns[c].Format is { } format)
            {
                sheet.Column(c + 1).Style.NumberFormat.Format = format;
            }
        }

        var header = sheet.Row(1);
        header.Style.Font.Bold = true;
        header.Style.NumberFormat.Format = "@";
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, rows.Count + 1, columns.Length).SetAutoFilter();
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed record Column<T>(string Header, Func<T, XLCellValue> Value, string? Format = null);
}
