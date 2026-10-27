using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Users;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Excel;

/// <summary>
/// GET .../export on the four list screens: the same filters as the list, every matching row, one sheet.
/// The tests share one database, so each filters on a code or name only it uses.
/// </summary>
public sealed class ExcelExportApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly HttpClient _client = api.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Products_ExportsEveryMatchingRow_WithOnHandAndAvailable_IgnoringPaging()
    {
        var oil = await _client.CreateProductAsync("XP-OIL", ProductType.RawMaterial, UnitOfMeasure.Liter);
        await _client.CreateProductAsync("XP-OIL-2", ProductType.RawMaterial);
        await _client.CreateProductAsync("XP-BOX", ProductType.Finished);
        var box = await _client.CreateProductAsync("XP-OILED-BOX", ProductType.Finished);
        await _client.AddBomLineAsync(box, oil, 0.5m);
        await _client.ReceiveAsync(oil, 4m);
        await CreateWorkOrderAsync(box, 2m);

        // pageSize=1 would cut a list response to one row; the export ignores paging.
        using var sheet = await ExportAsync("/products/export?search=XP-OIL&type=RawMaterial&pageSize=1", "products");

        Assert.Equal(["Code", "Name", "Type", "Unit", "On hand", "Available", "Minimum", "Active"], Headers(sheet));
        Assert.Equal(["XP-OIL", "XP-OIL-2"], Column(sheet, 1));
        var row = sheet.Worksheet.Row(2);
        Assert.Equal("Raw material", row.Cell(3).GetText());
        Assert.Equal("L", row.Cell(4).GetText());
        Assert.Equal(4d, row.Cell(5).GetDouble());
        Assert.Equal(3d, row.Cell(6).GetDouble());
        Assert.Equal("Yes", row.Cell(8).GetText());
    }

    [Fact]
    public async Task StockMovements_FiltersByProduct_NewestFirst()
    {
        var screw = await _client.CreateProductAsync("XS-SCREW", ProductType.RawMaterial);
        await _client.CreateProductAsync("XS-OTHER", ProductType.RawMaterial);
        await _client.ReceiveAsync(screw, 100m);
        var issue = await _client.PostAsJsonAsync("/stock/movements",
            new { productId = screw, type = "Issue", quantity = 30m, note = "Sample order" }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, issue.StatusCode);

        using var sheet = await ExportAsync($"/stock/movements/export?productId={screw}", "stock-movements");

        Assert.Equal(["Date (UTC)", "Code", "Product", "Type", "Quantity", "Unit", "Note", "By"], Headers(sheet));
        Assert.Equal(["Issue", "Receipt"], Column(sheet, 4));
        var row = sheet.Worksheet.Row(2);
        Assert.Equal(XLDataType.DateTime, row.Cell(1).DataType);
        Assert.Equal("XS-SCREW", row.Cell(2).GetText());
        Assert.Equal(-30d, row.Cell(5).GetDouble());
        Assert.Equal("Sample order", row.Cell(7).GetText());
    }

    [Fact]
    public async Task SalesOrders_FiltersLikeTheList_AndMarksLateOrders()
    {
        var chair = await _client.CreateProductAsync("XO-CHAIR", ProductType.Finished);
        var customer = await _client.CreateCustomerAsync("XO Export Café");
        var late = await CreateOrderAsync(customer, Today.AddDays(-3), chair);
        await CreateOrderAsync(customer, Today.AddDays(5), chair);

        using var sheet = await ExportAsync("/sales-orders/export?search=XO%20Export&late=true", "sales-orders");

        Assert.Equal(["Number", "Customer", "Order date", "Due date", "Status", "Late", "Shipped (UTC)"], Headers(sheet));
        var row = Assert.Single(sheet.Worksheet.RowsUsed().Skip(1));
        Assert.Equal(late, row.Cell(1).GetText());
        Assert.Equal("XO Export Café", row.Cell(2).GetText());
        Assert.Equal(Today.AddDays(-3).ToDateTime(TimeOnly.MinValue), row.Cell(4).GetDateTime());
        Assert.Equal("Open", row.Cell(5).GetText());
        Assert.Equal("Yes", row.Cell(6).GetText());
        Assert.True(row.Cell(7).IsEmpty());
    }

    [Fact]
    public async Task WorkOrders_FiltersLikeTheList_AndMarksMaterialShortage()
    {
        var shelf = await _client.CreateProductAsync("XW-SHELF", ProductType.Finished);
        var board = await _client.CreateProductAsync("XW-BOARD", ProductType.RawMaterial);
        await _client.AddBomLineAsync(shelf, board, 2m);
        var number = await CreateWorkOrderAsync(shelf, 3m);

        using var sheet = await ExportAsync("/work-orders/export?search=XW-SHELF&open=true", "work-orders");

        Assert.Equal(
            ["Number", "Code", "Product", "Quantity", "Unit", "Status", "Planned date", "Completed (UTC)", "Sales order", "Customer", "Material short"],
            Headers(sheet));
        var row = Assert.Single(sheet.Worksheet.RowsUsed().Skip(1));
        Assert.Equal(number, row.Cell(1).GetText());
        Assert.Equal(3d, row.Cell(4).GetDouble());
        Assert.Equal("pcs", row.Cell(5).GetText());
        Assert.Equal("Planned", row.Cell(6).GetText());
        Assert.Equal("Yes", row.Cell(11).GetText());
    }

    [Fact]
    public async Task Export_IsOpenToEveryRole_ButNeedsSignIn()
    {
        var warehouse = await api.CreateClientAs(UserRole.Warehouse).GetAsync("/work-orders/export", CancellationToken);
        var anonymous = await api.CreateAnonymousClient().GetAsync("/products/export", CancellationToken);

        Assert.Equal(HttpStatusCode.OK, warehouse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Export_InvalidFilter_Returns400()
    {
        var response = await _client.GetAsync("/products/export?type=Banana", CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<ExportedSheet> ExportAsync(string url, string fileNamePrefix)
    {
        var response = await _client.GetAsync(url, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(XlsxType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal($"{fileNamePrefix}-{Today:yyyy-MM-dd}.xlsx", response.Content.Headers.ContentDisposition?.FileName);

        var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync(CancellationToken));
        return new ExportedSheet(workbook, Assert.Single(workbook.Worksheets));
    }

    private static string[] Headers(ExportedSheet sheet) =>
        sheet.Worksheet.Row(1).CellsUsed().Select(c => c.GetText()).ToArray();

    private static string[] Column(ExportedSheet sheet, int column) =>
        sheet.Worksheet.RowsUsed().Skip(1).Select(r => r.Cell(column).GetText()).ToArray();

    private async Task<string> CreateOrderAsync(int customerId, DateOnly dueDate, int productId)
    {
        var response = await _client.PostAsJsonAsync("/sales-orders",
            new { customerId, orderDate = dueDate.AddDays(-10), dueDate, lines = new[] { new { productId, quantity = 1m } } },
            ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsync<NumberOnly>()).Number;
    }

    private async Task<string> CreateWorkOrderAsync(int productId, decimal quantity)
    {
        var response = await _client.PostAsJsonAsync("/work-orders", new { productId, quantity }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadAsync<NumberOnly>()).Number;
    }

    private sealed record NumberOnly(string Number);

    private sealed record ExportedSheet(XLWorkbook Workbook, IXLWorksheet Worksheet) : IDisposable
    {
        public void Dispose() => Workbook.Dispose();
    }
}
