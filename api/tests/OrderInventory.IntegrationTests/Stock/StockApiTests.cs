using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderInventory.Application.Stock;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Stock;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Stock;

public sealed class StockApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    // The tests in this class share one database, so every test uses its own code prefix.
    private readonly HttpClient _client = api.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    // ---------- recording movements ----------

    [Fact]
    public async Task Receipt_Returns201AndIncreasesOnHand()
    {
        var board = await CreateProductAsync("ST-REC");

        var response = await RecordAsync(board.Id, StockMovementType.Receipt, 12.5m, "Delivery note 4411");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var movement = await ReadAsync<MovementResponse>(response);
        Assert.Equal(board.Id, movement.ProductId);
        Assert.Equal("ST-REC", movement.ProductCode);
        Assert.Equal(StockMovementType.Receipt, movement.Type);
        Assert.Equal(12.5m, movement.Quantity);
        Assert.Equal("Delivery note 4411", movement.Note);
        Assert.False(string.IsNullOrWhiteSpace(movement.CreatedBy));
        Assert.Equal(12.5m, await OnHandAsync(board.Id));
    }

    [Fact]
    public async Task Issue_IsEnteredPositive_StoredNegative()
    {
        var board = await CreateProductAsync("ST-ISS");
        await RecordOkAsync(board.Id, StockMovementType.Receipt, 10m);

        var movement = await ReadAsync<MovementResponse>(await RecordAsync(board.Id, StockMovementType.Issue, 4m));

        Assert.Equal(-4m, movement.Quantity);
        Assert.Equal(6m, await OnHandAsync(board.Id));
    }

    [Fact]
    public async Task Adjustment_IsSigned()
    {
        var board = await CreateProductAsync("ST-ADJ");

        await RecordOkAsync(board.Id, StockMovementType.Adjustment, 2m);
        await RecordOkAsync(board.Id, StockMovementType.Adjustment, -0.75m);

        Assert.Equal(1.25m, await OnHandAsync(board.Id));
    }

    [Theory]
    [InlineData(StockMovementType.Issue, 5)]
    [InlineData(StockMovementType.Adjustment, -5)]
    public async Task MovementBelowZero_Returns422AndWritesNothing(StockMovementType type, int quantity)
    {
        // Rule 3: the operation is rejected as a whole and the ledger is unchanged.
        var board = await CreateProductAsync($"ST-NEG-{type}");
        await RecordOkAsync(board.Id, StockMovementType.Receipt, 3m);

        var response = await RecordAsync(board.Id, type, quantity);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, $"ST-NEG-{type}");
        Assert.Equal(3m, await OnHandAsync(board.Id));
        Assert.Equal(1, await CountMovementsAsync(board.Id));
    }

    [Fact]
    public async Task IssueOfEverything_LeavesZero()
    {
        var board = await CreateProductAsync("ST-ZERO");
        await RecordOkAsync(board.Id, StockMovementType.Receipt, 3m);

        await RecordOkAsync(board.Id, StockMovementType.Issue, 3m);

        Assert.Equal(0m, await OnHandAsync(board.Id));
    }

    [Theory]
    [InlineData("Receipt", 0)]
    [InlineData("Receipt", -1)]
    [InlineData("Issue", 0)]
    [InlineData("Issue", -2)]
    [InlineData("Adjustment", 0)]
    [InlineData("ProductionConsumption", -1)]
    [InlineData("ProductionOutput", 1)]
    public async Task InvalidTypeOrQuantity_Returns400(string type, int quantity)
    {
        // Production movements are written by work orders only (rule 10), never by hand.
        var board = await CreateProductAsync($"ST-400-{type}-{quantity}");

        var response = await _client.PostAsJsonAsync("/stock/movements",
            new { productId = board.Id, type, quantity }, ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountMovementsAsync(board.Id));
    }

    [Fact]
    public async Task NoteLongerThanColumn_Returns400()
    {
        var board = await CreateProductAsync("ST-NOTE");

        var response = await RecordAsync(board.Id, StockMovementType.Receipt, 1m, new string('x', 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownProduct_Returns422()
    {
        var response = await RecordAsync(999999, StockMovementType.Receipt, 1m);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "999999");
    }

    [Fact]
    public async Task CreatedBy_InTheBody_IsIgnored()
    {
        // CreatedBy comes from the authenticated user, never from the request.
        var board = await CreateProductAsync("ST-WHO");

        var response = await _client.PostAsJsonAsync("/stock/movements",
            new { productId = board.Id, type = "Receipt", quantity = 1m, createdBy = "mallory" },
            ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual("mallory", (await ReadAsync<MovementResponse>(response)).CreatedBy);
    }

    // ---------- listing ----------

    [Fact]
    public async Task List_ForProduct_ReturnsNewestFirstAndPages()
    {
        var board = await CreateProductAsync("ST-LIST");
        var other = await CreateProductAsync("ST-LIST-OTHER");
        await RecordOkAsync(board.Id, StockMovementType.Receipt, 10m);
        await RecordOkAsync(other.Id, StockMovementType.Receipt, 99m);
        await RecordOkAsync(board.Id, StockMovementType.Issue, 1m);
        await RecordOkAsync(board.Id, StockMovementType.Issue, 2m);

        var page = await _client.GetFromJsonAsync<PagedResponse<MovementResponse>>(
            $"/stock/movements?productId={board.Id}&pageSize=2", ApiFactory.JsonOptions, CancellationToken);

        Assert.Equal([-2m, -1m], page!.Items.Select(m => m.Quantity));
        Assert.Equal(3, page.TotalCount);
    }

    // ---------- append-only (rule 2) ----------

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Movements_CannotBeChangedOrDeletedOverHttp(string method)
    {
        var board = await CreateProductAsync($"ST-RO-{method}");
        var movement = await ReadAsync<MovementResponse>(await RecordAsync(board.Id, StockMovementType.Receipt, 5m));

        var response = await _client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), $"/stock/movements/{movement.Id}")
            {
                Content = JsonContent.Create(new { quantity = 50m }),
            },
            CancellationToken);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(5m, await OnHandAsync(board.Id));
    }

    [Fact]
    public async Task Movements_CannotBeChangedThroughTheDbContext()
    {
        var board = await CreateProductAsync("ST-RO-DB-UPD");
        var movement = await ReadAsync<MovementResponse>(await RecordAsync(board.Id, StockMovementType.Receipt, 5m));
        await using var scope = api.CreateScope();
        var dbContext = ApiFactory.GetDbContext(scope);
        var entity = await dbContext.StockMovements.SingleAsync(m => m.Id == movement.Id, CancellationToken);

        dbContext.Entry(entity).Property(m => m.Quantity).CurrentValue = 50m;

        await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync(CancellationToken));
    }

    [Fact]
    public async Task Movements_CannotBeDeletedThroughTheDbContext()
    {
        var board = await CreateProductAsync("ST-RO-DB-DEL");
        var movement = await ReadAsync<MovementResponse>(await RecordAsync(board.Id, StockMovementType.Receipt, 5m));
        await using var scope = api.CreateScope();
        var dbContext = ApiFactory.GetDbContext(scope);
        var entity = await dbContext.StockMovements.SingleAsync(m => m.Id == movement.Id, CancellationToken);

        dbContext.StockMovements.Remove(entity);

        await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync(CancellationToken));
    }

    // ---------- concurrency (rule 4) ----------

    [Fact]
    public async Task ConcurrentIssues_OfTheSameStock_OnlyOneSucceeds()
    {
        // 10 on hand, two issues of 7 at the same moment. Each would pass the check on its own;
        // only the row lock makes the second one see the first one's movement.
        for (var i = 0; i < 10; i++)
        {
            var board = await CreateProductAsync($"ST-RACE-{i}");
            await RecordOkAsync(board.Id, StockMovementType.Receipt, 10m);

            var responses = await Task.WhenAll(
                RecordAsync(board.Id, StockMovementType.Issue, 7m),
                RecordAsync(board.Id, StockMovementType.Issue, 7m));

            Assert.Equal(
                [HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity],
                responses.Select(r => r.StatusCode).Order());
            Assert.Equal(3m, await OnHandAsync(board.Id));
        }
    }

    [Fact]
    public async Task ManyConcurrentIssues_NeverTakeMoreThanOnHand()
    {
        var board = await CreateProductAsync("ST-RACE-MANY");
        await RecordOkAsync(board.Id, StockMovementType.Receipt, 5m);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(_ => RecordAsync(board.Id, StockMovementType.Issue, 1m)));

        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity));
        Assert.Equal(0m, await OnHandAsync(board.Id));
    }

    [Fact]
    public async Task ConcurrentMultiProductPosts_InOppositeOrder_DoNotDeadlock()
    {
        // Rule 4 locks rows ordered by product id. Two operations touching A and B, listed in
        // opposite order, must both complete instead of one being aborted as a deadlock.
        var a = await CreateProductAsync("ST-LOCK-A");
        var b = await CreateProductAsync("ST-LOCK-B");
        await RecordOkAsync(a.Id, StockMovementType.Receipt, 100m);
        await RecordOkAsync(b.Id, StockMovementType.Receipt, 100m);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => PostThroughLedgerAsync(i % 2 == 0
            ? [new(a.Id, -1m, StockMovementType.Issue), new(b.Id, -1m, StockMovementType.Issue)]
            : [new(b.Id, -1m, StockMovementType.Issue), new(a.Id, -1m, StockMovementType.Issue)])));

        Assert.Equal(80m, await OnHandAsync(a.Id));
        Assert.Equal(80m, await OnHandAsync(b.Id));
    }

    [Fact]
    public async Task LedgerPost_WithOneShortProduct_WritesNothingForAnyProduct()
    {
        var a = await CreateProductAsync("ST-ALL-A");
        var b = await CreateProductAsync("ST-ALL-B");
        await RecordOkAsync(a.Id, StockMovementType.Receipt, 10m);
        await RecordOkAsync(b.Id, StockMovementType.Receipt, 1m);

        await Assert.ThrowsAsync<OrderInventory.Application.Common.BusinessRuleException>(() => PostThroughLedgerAsync(
            [new(a.Id, -4m, StockMovementType.Issue), new(b.Id, -2m, StockMovementType.Issue)]));

        Assert.Equal(10m, await OnHandAsync(a.Id));
        Assert.Equal(1m, await OnHandAsync(b.Id));
    }

    [Fact]
    public async Task LedgerPost_WrongSignForType_IsRefused()
    {
        var a = await CreateProductAsync("ST-SIGN");

        await Assert.ThrowsAsync<ArgumentException>(() => PostThroughLedgerAsync(
            [new(a.Id, -4m, StockMovementType.Receipt)]));
    }

    // ---------- helpers ----------

    private async Task<ProductResponse> CreateProductAsync(string code)
    {
        var response = await _client.PostAsJsonAsync("/products",
            new { code, name = $"Product {code}", type = ProductType.RawMaterial, unit = UnitOfMeasure.Meter, minStock = 0m },
            ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<ProductResponse>(response);
    }

    private Task<HttpResponseMessage> RecordAsync(int productId, StockMovementType type, decimal quantity, string? note = null) =>
        _client.PostAsJsonAsync("/stock/movements", new { productId, type, quantity, note }, ApiFactory.JsonOptions, CancellationToken);

    private async Task RecordOkAsync(int productId, StockMovementType type, decimal quantity)
    {
        var response = await RecordAsync(productId, type, quantity);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task PostThroughLedgerAsync(NewStockMovement[] movements)
    {
        await using var scope = api.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StockLedger>().PostAsync(movements, CancellationToken);
    }

    private async Task<decimal> OnHandAsync(int productId) =>
        (await _client.GetFromJsonAsync<ProductResponse>($"/products/{productId}", ApiFactory.JsonOptions, CancellationToken))!.OnHand;

    private async Task<int> CountMovementsAsync(int productId)
    {
        await using var scope = api.CreateScope();
        return await ApiFactory.GetDbContext(scope).StockMovements.CountAsync(m => m.ProductId == productId, CancellationToken);
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

    private sealed record ProductResponse(int Id, string Code, decimal OnHand);

    private sealed record MovementResponse(
        int Id, int ProductId, string ProductCode, string ProductName, UnitOfMeasure Unit, StockMovementType Type,
        decimal Quantity, StockMovementRefType? RefType, int? RefId, string? Note, string CreatedBy, DateTime CreatedAt);

    private sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
}
