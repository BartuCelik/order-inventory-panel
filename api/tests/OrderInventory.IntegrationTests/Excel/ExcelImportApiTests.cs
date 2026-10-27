using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Stock;
using OrderInventory.Domain.Users;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Excel;

/// <summary>
/// POST /products/import and /stock/opening-stock. Both check the whole file first and write all of it or
/// nothing; <c>?dryRun=true</c> answers the same without writing. The tests share one database, so each uses
/// its own code prefix.
/// </summary>
public sealed class ExcelImportApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static readonly string[] ProductHeaders = ["Code", "Name", "Type", "Unit", "Minimum", "Active"];

    private readonly HttpClient _client = api.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    // ---------- products ----------

    [Fact]
    public async Task Products_CreatesNewCodes_AndUpdatesExistingOnes()
    {
        var existing = await _client.CreateProductAsync("IP-EXIST", ProductType.RawMaterial, UnitOfMeasure.Kilogram);

        var response = await ImportProductsAsync(
            ["ip-new", " New oak bench ", "Finished", "pcs", 2, "Yes"],
            ["IP-EXIST", "Renamed glue", "Raw material", "kg", 5, "No"]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new ImportResponse(1, 1, 0, false), await response.ReadAsync<ImportResponse>());

        await using var scope = api.CreateScope();
        var products = await ApiFactory.GetDbContext(scope).Products.AsNoTracking()
            .Where(p => p.Code == "IP-NEW" || p.Code == "IP-EXIST").ToDictionaryAsync(p => p.Code, CancellationToken);
        var created = products["IP-NEW"];
        Assert.Equal(("New oak bench", ProductType.Finished, UnitOfMeasure.Piece, 2m, true),
            (created.Name, created.Type, created.Unit, created.MinStock, created.IsActive));
        var updated = products["IP-EXIST"];
        Assert.Equal((existing, "Renamed glue", 5m, false), (updated.Id, updated.Name, updated.MinStock, updated.IsActive));
    }

    [Fact]
    public async Task Products_DryRun_ReportsTheSameCounts_ButWritesNothing()
    {
        var response = await ImportProductsAsync(dryRun: true, ["IP-DRY", "Dry run stool", "Finished", "pcs", null, null]);

        Assert.Equal(new ImportResponse(1, 0, 0, true), await response.ReadAsync<ImportResponse>());
        Assert.False(await ProductExistsAsync("IP-DRY"));
    }

    [Fact]
    public async Task Products_AnyBadRow_ImportsNothing_AndListsEveryErrorByRow()
    {
        await _client.CreateProductAsync("IP-TYPE", ProductType.RawMaterial);

        var response = await ImportProductsAsync(
            ["IP-OK", "Would be fine", "Finished", "pcs", 0, "Yes"],
            ["IP-BAD", "", "Gadget", "box", -1, "maybe"],
            ["IP-OK", "Same code again", "Finished", "pcs", 0, "Yes"],
            ["IP-TYPE", "Type changed", "Finished", "pcs", 0, "Yes"]);

        var errors = await AssertImportErrorsAsync(response);
        Assert.Contains("Row 3", errors.Keys);
        Assert.Equal(5, errors["Row 3"].Length); // name, type, unit, minimum, active
        Assert.Contains("IP-OK", Assert.Single(errors["Row 4"]), StringComparison.Ordinal);
        Assert.Contains("type", Assert.Single(errors["Row 5"]), StringComparison.OrdinalIgnoreCase);
        Assert.False(await ProductExistsAsync("IP-OK"));
    }

    [Fact]
    public async Task Products_ExportedFile_ImportsBackUnchanged()
    {
        await _client.CreateProductAsync("IP-ROUND", ProductType.SemiFinished, UnitOfMeasure.SquareMeter);
        var exported = await _client.GetByteArrayAsync("/products/export?search=IP-ROUND", CancellationToken);

        var response = await PostFileAsync("/products/import", exported);

        Assert.Equal(new ImportResponse(0, 0, 1, false), await response.ReadAsync<ImportResponse>());
    }

    [Fact]
    public async Task Products_MissingColumn_IsAFileError()
    {
        var response = await PostFileAsync("/products/import", Workbook(["Code", "Name"], ["IP-COLS", "No type"]));

        var errors = await AssertImportErrorsAsync(response);
        Assert.Collection(errors["File"],
            e => Assert.Contains("'Type'", e, StringComparison.Ordinal),
            e => Assert.Contains("'Unit'", e, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_NotAnExcelFile_Returns400()
    {
        var response = await PostFileAsync("/products/import", "Code,Name\nX,Y"u8.ToArray());

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "Excel");
    }

    [Fact]
    public async Task Products_NeedPlanningRole()
    {
        var file = Workbook(ProductHeaders, ["IP-ROLE", "Role test", "Finished", "pcs", 0, "Yes"]);

        var response = await PostFileAsync("/products/import", file, api.CreateClientAs(UserRole.Warehouse));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- opening stock ----------

    [Fact]
    public async Task OpeningStock_WritesOneAdjustmentPerProduct_SkippingZeros()
    {
        var oak = await _client.CreateProductAsync("IS-OAK", ProductType.RawMaterial, UnitOfMeasure.SquareMeter);
        var felt = await _client.CreateProductAsync("IS-FELT", ProductType.RawMaterial);

        var response = await ImportStockAsync(["is-oak", 12.5], ["IS-FELT", 0]);

        Assert.Equal(new ImportResponse(1, 0, 1, false), await response.ReadAsync<ImportResponse>());
        await using var scope = api.CreateScope();
        var movement = await ApiFactory.GetDbContext(scope).StockMovements.AsNoTracking()
            .SingleAsync(m => m.ProductId == oak, CancellationToken);
        Assert.Equal((StockMovementType.Adjustment, 12.5m, "Opening stock"), (movement.Type, movement.Quantity, movement.Note));
        Assert.Equal(0m, await _client.OnHandAsync(felt));
    }

    [Fact]
    public async Task OpeningStock_ProductWithMovements_ImportsNothing()
    {
        var used = await _client.CreateProductAsync("IS-USED", ProductType.RawMaterial);
        var fresh = await _client.CreateProductAsync("IS-FRESH", ProductType.RawMaterial);
        await _client.ReceiveAsync(used, 3m);

        var response = await ImportStockAsync(["IS-FRESH", 4], ["IS-USED", 10], ["IS-NOPE", 1], ["IS-FRESH", -2]);

        var errors = await AssertImportErrorsAsync(response);
        Assert.Contains("IS-USED", Assert.Single(errors["Row 3"]), StringComparison.Ordinal);
        Assert.Contains("IS-NOPE", Assert.Single(errors["Row 4"]), StringComparison.Ordinal);
        Assert.Equal(2, errors["Row 5"].Length); // repeated code and negative quantity
        Assert.Equal((3m, 0m), (await _client.OnHandAsync(used), await _client.OnHandAsync(fresh)));
    }

    [Fact]
    public async Task OpeningStock_DryRun_WritesNothing()
    {
        var board = await _client.CreateProductAsync("IS-DRY", ProductType.RawMaterial);

        var response = await ImportStockAsync(dryRun: true, ["IS-DRY", 7]);

        Assert.Equal(new ImportResponse(1, 0, 0, true), await response.ReadAsync<ImportResponse>());
        Assert.Equal(0m, await _client.OnHandAsync(board));
    }

    [Fact]
    public async Task OpeningStock_TheSameFileTwiceAtOnce_IsWrittenOnce()
    {
        // Rule 4: the "no movements yet" check runs inside the product row lock. Without it both requests
        // would see an empty ledger and the opening stock would be doubled.
        var screw = await _client.CreateProductAsync("IS-RACE", ProductType.RawMaterial);
        var file = Workbook(["Code", "Quantity"], ["IS-RACE", 100]);

        var responses = await Task.WhenAll(
            PostFileAsync("/stock/opening-stock", file), PostFileAsync("/stock/opening-stock", file));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal(100m, await _client.OnHandAsync(screw));
    }

    [Fact]
    public async Task OpeningStock_NeedsWarehouseRole()
    {
        var response = await PostFileAsync("/stock/opening-stock",
            Workbook(["Code", "Quantity"], ["IS-ROLE", 1]), api.CreateClientAs(UserRole.Planner));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- helpers ----------

    private Task<HttpResponseMessage> ImportProductsAsync(params object?[][] rows) => ImportProductsAsync(false, rows);

    private Task<HttpResponseMessage> ImportProductsAsync(bool dryRun, params object?[][] rows) =>
        PostFileAsync($"/products/import?dryRun={dryRun}", Workbook(ProductHeaders, rows));

    private Task<HttpResponseMessage> ImportStockAsync(params object?[][] rows) => ImportStockAsync(false, rows);

    private Task<HttpResponseMessage> ImportStockAsync(bool dryRun, params object?[][] rows) =>
        PostFileAsync($"/stock/opening-stock?dryRun={dryRun}", Workbook(["Code", "Quantity"], rows));

    private async Task<HttpResponseMessage> PostFileAsync(string url, byte[] file, HttpClient? client = null)
    {
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(file);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(part, "file", "import.xlsx");
        return await (client ?? _client).PostAsync(url, content, CancellationToken);
    }

    /// <summary>A workbook as a user would type it: numbers as numbers, text as text, blanks left empty.</summary>
    private static byte[] Workbook(string[] headers, params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Import");
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(rows[r][c], CultureInfo.InvariantCulture);
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static async Task<Dictionary<string, string[]>> AssertImportErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ImportProblem>(ApiFactory.JsonOptions, CancellationToken);
        Assert.Contains("nothing was imported", problem!.Detail, StringComparison.OrdinalIgnoreCase);
        return problem.Errors;
    }

    private async Task<bool> ProductExistsAsync(string code)
    {
        await using var scope = api.CreateScope();
        return await ApiFactory.GetDbContext(scope).Products.AnyAsync(p => p.Code == code, CancellationToken);
    }

    private sealed record ImportResponse(int Created, int Updated, int Unchanged, bool DryRun);

    private sealed record ImportProblem(string Detail, Dictionary<string, string[]> Errors);
}
