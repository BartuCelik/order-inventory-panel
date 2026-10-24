using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Domain.Products;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Products;

public sealed class BomApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    // The tests in this class share one database, so every test uses its own code prefix.
    private readonly HttpClient _client = api.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    // ---------- GET bom ----------

    [Fact]
    public async Task GetBom_ReturnsDirectLinesWithComponentDetails_OrderedByComponentCode()
    {
        var chair = await CreateProductAsync("GB-CHAIR", ProductType.Finished);
        var seat = await CreateProductAsync("GB-SEAT", ProductType.SemiFinished);
        var screw = await CreateProductAsync("GB-A-SCREW", ProductType.RawMaterial, UnitOfMeasure.Piece);
        var plywood = await CreateProductAsync("GB-PLY", ProductType.RawMaterial);
        await AddLineAsync(chair.Id, seat.Id, 1m);
        await AddLineAsync(chair.Id, screw.Id, 8m);
        await AddLineAsync(seat.Id, plywood.Id, 0.25m);

        var lines = await _client.GetFromJsonAsync<List<BomLineResponse>>(
            $"/api/products/{chair.Id}/bom", ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(["GB-A-SCREW", "GB-SEAT"], lines!.Select(l => l.ComponentCode));
        var screwLine = lines![0];
        Assert.Equal(screw.Id, screwLine.ComponentProductId);
        Assert.Equal(screw.Name, screwLine.ComponentName);
        Assert.Equal(ProductType.RawMaterial, screwLine.ComponentType);
        Assert.Equal(UnitOfMeasure.Piece, screwLine.ComponentUnit);
        Assert.Equal(8m, screwLine.QtyPerUnit);
    }

    [Fact]
    public async Task GetBom_UnknownProduct_Returns404()
    {
        var response = await _client.GetAsync("/api/products/999999/bom", CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "999999");
    }

    // ---------- POST bom line ----------

    [Fact]
    public async Task PostLine_Valid_Returns201WithLine()
    {
        var table = await CreateProductAsync("PL-TABLE", ProductType.Finished);
        var leg = await CreateProductAsync("PL-LEG", ProductType.SemiFinished, UnitOfMeasure.Piece);

        var response = await PostLineAsync(table.Id, leg.Id, 4m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var line = await ReadAsync<BomLineResponse>(response);
        Assert.Equal(new BomLineResponse(line.Id, leg.Id, "PL-LEG", leg.Name, ProductType.SemiFinished, UnitOfMeasure.Piece, 4m), line);
        Assert.Equal($"/api/products/{table.Id}/bom", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task PostLine_RawMaterialParent_Returns422()
    {
        var board = await CreateProductAsync("PL-RAW-BOARD", ProductType.RawMaterial);
        var glue = await CreateProductAsync("PL-RAW-GLUE", ProductType.RawMaterial);

        var response = await PostLineAsync(board.Id, glue.Id, 1m);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "raw material");
    }

    [Fact]
    public async Task PostLine_DuplicateComponent_Returns409()
    {
        var shelf = await CreateProductAsync("PL-DUP-SHELF", ProductType.Finished);
        var pin = await CreateProductAsync("PL-DUP-PIN", ProductType.RawMaterial);
        await AddLineAsync(shelf.Id, pin.Id, 4m);

        var response = await PostLineAsync(shelf.Id, pin.Id, 2m);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "PL-DUP-PIN");
    }

    [Fact]
    public async Task PostLine_IndirectCycle_Returns422()
    {
        // A -> B -> C, then C -> A would close the loop.
        var a = await CreateProductAsync("PL-CYC-A", ProductType.Finished);
        var b = await CreateProductAsync("PL-CYC-B", ProductType.SemiFinished);
        var c = await CreateProductAsync("PL-CYC-C", ProductType.SemiFinished);
        await AddLineAsync(a.Id, b.Id, 1m);
        await AddLineAsync(b.Id, c.Id, 1m);

        var response = await PostLineAsync(c.Id, a.Id, 1m);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "cycle");
    }

    [Fact]
    public async Task PostLine_ProductAsItsOwnComponent_Returns422()
    {
        var frame = await CreateProductAsync("PL-SELF", ProductType.SemiFinished);

        var response = await PostLineAsync(frame.Id, frame.Id, 1m);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "cycle");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task PostLine_NonPositiveQtyPerUnit_Returns400(int qtyPerUnit)
    {
        var drawer = await CreateProductAsync($"PL-QTY{qtyPerUnit}-DRAWER", ProductType.SemiFinished);
        var rail = await CreateProductAsync($"PL-QTY{qtyPerUnit}-RAIL", ProductType.RawMaterial);

        var response = await PostLineAsync(drawer.Id, rail.Id, qtyPerUnit);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("QtyPerUnit", (await ReadAsync<ValidationProblemDetails>(response)).Errors.Keys);
    }

    [Fact]
    public async Task PostLine_UnknownParent_Returns404()
    {
        var hinge = await CreateProductAsync("PL-404-HINGE", ProductType.RawMaterial);

        var response = await PostLineAsync(999999, hinge.Id, 1m);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "999999");
    }

    [Fact]
    public async Task PostLine_UnknownComponent_Returns422()
    {
        var door = await CreateProductAsync("PL-422-DOOR", ProductType.SemiFinished);

        var response = await PostLineAsync(door.Id, 999999, 1m);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "999999");
    }

    [Fact]
    public async Task PostLine_ConcurrentOppositeLines_OnlyOneSucceeds()
    {
        // Rule 6 under concurrency: A -> B and B -> A each pass the cycle check on their own.
        // If both were checked before either was committed, the BOM would end up with a cycle.
        for (var i = 0; i < 10; i++)
        {
            var a = await CreateProductAsync($"PL-RACE{i}-A", ProductType.SemiFinished);
            var b = await CreateProductAsync($"PL-RACE{i}-B", ProductType.SemiFinished);

            var responses = await Task.WhenAll(PostLineAsync(a.Id, b.Id, 1m), PostLineAsync(b.Id, a.Id, 1m));

            Assert.Equal(
                [HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity],
                responses.Select(r => r.StatusCode).Order());
            await using var scope = api.CreateScope();
            var lineCount = await ApiFactory.GetDbContext(scope).BomLines
                .CountAsync(l => l.ParentProductId == a.Id || l.ParentProductId == b.Id, CancellationToken);
            Assert.Equal(1, lineCount);
        }
    }

    // ---------- DELETE bom line ----------

    [Fact]
    public async Task DeleteLine_Existing_Returns204AndRemovesIt()
    {
        var cabinet = await CreateProductAsync("DL-CAB", ProductType.Finished);
        var knob = await CreateProductAsync("DL-KNOB", ProductType.RawMaterial);
        var line = await AddLineAsync(cabinet.Id, knob.Id, 2m);

        var response = await _client.DeleteAsync($"/api/products/{cabinet.Id}/bom/{line.Id}", CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var lines = await _client.GetFromJsonAsync<List<BomLineResponse>>(
            $"/api/products/{cabinet.Id}/bom", ApiFactory.JsonOptions, CancellationToken);
        Assert.Empty(lines!);
    }

    [Fact]
    public async Task DeleteLine_OfAnotherProduct_Returns404()
    {
        var cabinet = await CreateProductAsync("DL-OTHER-CAB", ProductType.Finished);
        var stool = await CreateProductAsync("DL-OTHER-STOOL", ProductType.Finished);
        var knob = await CreateProductAsync("DL-OTHER-KNOB", ProductType.RawMaterial);
        var line = await AddLineAsync(cabinet.Id, knob.Id, 2m);

        var response = await _client.DeleteAsync($"/api/products/{stool.Id}/bom/{line.Id}", CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, line.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    // ---------- GET explosion ----------

    [Fact]
    public async Task Explosion_SumsRawMaterialsThroughSemiFinishedComponents()
    {
        // Bench = Seat x2 + Screw x8;  Seat = Plywood x0.5 + Screw x4  ->  per bench: Plywood 1, Screw 16
        var bench = await CreateProductAsync("EX-BENCH", ProductType.Finished);
        var seat = await CreateProductAsync("EX-SEAT", ProductType.SemiFinished);
        var screw = await CreateProductAsync("EX-SCREW", ProductType.RawMaterial, UnitOfMeasure.Piece);
        var plywood = await CreateProductAsync("EX-PLY", ProductType.RawMaterial, UnitOfMeasure.SquareMeter);
        await AddLineAsync(bench.Id, seat.Id, 2m);
        await AddLineAsync(bench.Id, screw.Id, 8m);
        await AddLineAsync(seat.Id, plywood.Id, 0.5m);
        await AddLineAsync(seat.Id, screw.Id, 4m);

        var explosion = await _client.GetFromJsonAsync<ExplosionResponse>(
            $"/api/products/{bench.Id}/bom/explosion?qty=2.5", ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(bench.Id, explosion!.ProductId);
        Assert.Equal(2.5m, explosion.Quantity);
        Assert.Equal(
            [
                new ExplosionItemResponse(plywood.Id, "EX-PLY", plywood.Name, ProductType.RawMaterial, UnitOfMeasure.SquareMeter, 2.5m),
                new ExplosionItemResponse(screw.Id, "EX-SCREW", screw.Name, ProductType.RawMaterial, UnitOfMeasure.Piece, 40m),
            ],
            explosion.Items);
    }

    [Fact]
    public async Task Explosion_ProductWithoutBom_ReturnsNoItems()
    {
        var glue = await CreateProductAsync("EX-EMPTY-GLUE", ProductType.RawMaterial);

        var explosion = await _client.GetFromJsonAsync<ExplosionResponse>(
            $"/api/products/{glue.Id}/bom/explosion?qty=1", ApiFactory.JsonOptions, CancellationToken);

        Assert.Empty(explosion!.Items);
    }

    [Theory]
    [InlineData("qty=0", "EX-400-ZERO")]
    [InlineData("qty=-3", "EX-400-NEG")]
    [InlineData("", "EX-400-MISSING")]
    public async Task Explosion_MissingOrNonPositiveQty_Returns400(string query, string code)
    {
        var desk = await CreateProductAsync(code, ProductType.Finished);

        var response = await _client.GetAsync($"/api/products/{desk.Id}/bom/explosion?{query}", CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Explosion_UnknownProduct_Returns404()
    {
        var response = await _client.GetAsync("/api/products/999999/bom/explosion?qty=1", CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "999999");
    }

    // ---------- helpers ----------

    private async Task<ProductResponse> CreateProductAsync(string code, ProductType type, UnitOfMeasure unit = UnitOfMeasure.Meter)
    {
        var response = await _client.PostAsJsonAsync("/api/products",
            new { code, name = $"Product {code}", type, unit, minStock = 0m }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<ProductResponse>(response);
    }

    private Task<HttpResponseMessage> PostLineAsync(int parentId, int componentId, decimal qtyPerUnit) =>
        _client.PostAsJsonAsync($"/api/products/{parentId}/bom",
            new { componentProductId = componentId, qtyPerUnit }, ApiFactory.JsonOptions, CancellationToken);

    private async Task<BomLineResponse> AddLineAsync(int parentId, int componentId, decimal qtyPerUnit)
    {
        var response = await PostLineAsync(parentId, componentId, qtyPerUnit);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<BomLineResponse>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiFactory.JsonOptions, CancellationToken))!;

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus, string detailContains)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync<ProblemDetails>(response);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Contains(detailContains, problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ProductResponse(int Id, string Code, string Name);

    private sealed record BomLineResponse(
        int Id, int ComponentProductId, string ComponentCode, string ComponentName,
        ProductType ComponentType, UnitOfMeasure ComponentUnit, decimal QtyPerUnit);

    private sealed record ExplosionResponse(int ProductId, decimal Quantity, IReadOnlyList<ExplosionItemResponse> Items);

    private sealed record ExplosionItemResponse(
        int ProductId, string Code, string Name, ProductType Type, UnitOfMeasure Unit, decimal Quantity);
}
