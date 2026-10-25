using System.Net;
using System.Net.Http.Json;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Stock;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Dashboard;

/// <summary>
/// GET /dashboard. This class has its own database (one ApiFactory per class), so the counts only
/// include what these tests create; each test still asserts on its own products or on deltas.
/// </summary>
public sealed class DashboardApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = api.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProductCounts_CountActiveProductsPerTypeAndInactiveSeparately()
    {
        var before = await GetAsync();
        await CreateProductAsync("DB-CNT-RAW", ProductType.RawMaterial);
        await CreateProductAsync("DB-CNT-FIN", ProductType.Finished);
        var inactive = await CreateProductAsync("DB-CNT-OFF", ProductType.SemiFinished);
        await DeactivateAsync(inactive);

        var after = await GetAsync();

        Assert.Equal(before.Products.RawMaterial + 1, after.Products.RawMaterial);
        Assert.Equal(before.Products.SemiFinished, after.Products.SemiFinished);
        Assert.Equal(before.Products.Finished + 1, after.Products.Finished);
        Assert.Equal(before.Products.Inactive + 1, after.Products.Inactive);
    }

    [Fact]
    public async Task BelowMinimum_ListsActiveProductsUnderTheirMinimum_MostCriticalFirst()
    {
        var empty = await CreateProductAsync("DB-MIN-EMPTY", ProductType.RawMaterial, minStock: 10m);
        var low = await CreateProductAsync("DB-MIN-LOW", ProductType.RawMaterial, minStock: 10m);
        var atMin = await CreateProductAsync("DB-MIN-AT", ProductType.RawMaterial, minStock: 10m);
        var noMin = await CreateProductAsync("DB-MIN-NONE", ProductType.RawMaterial, minStock: 0m);
        var inactive = await CreateProductAsync("DB-MIN-OFF", ProductType.RawMaterial, minStock: 10m);
        await DeactivateAsync(inactive);
        await ReceiveAsync(low, 7.5m);
        await ReceiveAsync(atMin, 10m);

        var dashboard = await GetAsync();
        var mine = dashboard.BelowMinimum.Where(p => p.Code.StartsWith("DB-MIN-", StringComparison.Ordinal)).ToList();

        // At the minimum is not below it; no minimum means never critical; inactive products are not tracked.
        Assert.Equal(["DB-MIN-EMPTY", "DB-MIN-LOW"], mine.Select(p => p.Code));
        Assert.Equal((empty, 0m, 0m, 10m), (mine[0].ProductId, mine[0].OnHand, mine[0].Available, mine[0].MinStock));
        Assert.Equal((low, 7.5m), (mine[1].ProductId, mine[1].OnHand));
        Assert.DoesNotContain(dashboard.BelowMinimum, p => p.ProductId == noMin);
    }

    [Fact]
    public async Task BelowMinimum_ComparesAvailableStock_OnHandMinusOpenWorkOrderReservations()
    {
        var board = await CreateProductAsync("DB-RES-BOARD", ProductType.RawMaterial, minStock: 10m);
        var shelf = await CreateProductAsync("DB-RES-SHELF", ProductType.Finished);
        var bom = await _client.PostAsJsonAsync($"/products/{shelf}/bom-lines",
            new { componentProductId = board, qtyPerUnit = 2m }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, bom.StatusCode);
        await ReceiveAsync(board, 15m);
        await CreateWorkOrderAsync(shelf, 4m);
        var cancelled = await CreateWorkOrderAsync(shelf, 100m);
        var cancel = await _client.PostAsync($"/work-orders/{cancelled}/cancel", null, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var dashboard = await GetAsync();

        // 15 on hand is above the minimum, but the open work order reserves 8 (the cancelled one reserves nothing).
        var row = Assert.Single(dashboard.BelowMinimum, p => p.ProductId == board);
        Assert.Equal((15m, 7m, 10m), (row.OnHand, row.Available, row.MinStock));
    }

    [Fact]
    public async Task RecentMovements_NewestFirst_AtMostEight()
    {
        var glue = await CreateProductAsync("DB-REC-GLUE", ProductType.RawMaterial);
        for (var i = 1; i <= 9; i++)
        {
            await ReceiveAsync(glue, i);
        }

        var recent = (await GetAsync()).RecentMovements;

        Assert.Equal(8, recent.Count);
        Assert.Equal(9m, recent[0].Quantity);
        Assert.Equal("DB-REC-GLUE", recent[0].ProductCode);
    }

    [Fact]
    public async Task Activity_HasFourteenDaysEndingToday_CountingInAndOutMovements()
    {
        var before = await GetAsync();
        var oil = await CreateProductAsync("DB-ACT-OIL", ProductType.RawMaterial);
        await ReceiveAsync(oil, 5m);
        await ReceiveAsync(oil, 1m);
        await IssueAsync(oil, 2m);

        var activity = (await GetAsync()).Activity;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(14, activity.Count);
        Assert.Equal(today.AddDays(-13), activity[0].Date);
        Assert.Equal(today, activity[^1].Date);
        Assert.Equal(before.Activity[^1].In + 2, activity[^1].In);
        Assert.Equal(before.Activity[^1].Out + 1, activity[^1].Out);
    }

    [Fact]
    public async Task OrdersAndProduction_CountOpenLateAndShortWork()
    {
        var before = await GetAsync();
        var table = await CreateProductAsync("DB-OP-TABLE", ProductType.Finished);
        var leg = await CreateProductAsync("DB-OP-LEG", ProductType.SemiFinished);
        var bom = await _client.PostAsJsonAsync($"/products/{table}/bom-lines",
            new { componentProductId = leg, qtyPerUnit = 4m }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, bom.StatusCode);
        var customerResponse = await _client.PostAsJsonAsync("/customers", new { name = "DB-OP Customer" }, ApiFactory.JsonOptions, CancellationToken);
        var customer = (await customerResponse.Content.ReadFromJsonAsync<IdOnly>(ApiFactory.JsonOptions, CancellationToken))!.Id;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var late = await CreateOrderAsync(customer, table, today.AddDays(-10), today.AddDays(-2));
        await CreateOrderAsync(customer, table, today, today.AddDays(3));
        // Nothing on hand: the work order is short (rule 9).
        var workOrder = await _client.PostAsJsonAsync("/work-orders", new { productId = table, quantity = 1m }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, workOrder.StatusCode);

        var after = await GetAsync();

        Assert.Equal(before.Orders.Open + 2, after.Orders.Open);
        Assert.Equal(before.Orders.Late + 1, after.Orders.Late);
        Assert.Equal(late, after.Orders.DueSoon[0].Id);
        Assert.True(after.Orders.DueSoon[0].IsLate);
        Assert.Equal(before.Production.Planned + 1, after.Production.Planned);
        Assert.Equal(before.Production.ShortCount + 1, after.Production.ShortCount);
        Assert.Contains(after.Production.Queue, w => w.ProductCode == "DB-OP-TABLE" && w.HasShortage);
    }

    // ---------- helpers ----------

    private async Task<DashboardResponse> GetAsync()
    {
        var response = await _client.GetAsync("/dashboard", CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DashboardResponse>(ApiFactory.JsonOptions, CancellationToken))!;
    }

    private async Task<int> CreateProductAsync(string code, ProductType type, decimal minStock = 0m)
    {
        var response = await _client.PostAsJsonAsync("/products",
            new { code, name = $"Product {code}", type, unit = UnitOfMeasure.Piece, minStock }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdOnly>(ApiFactory.JsonOptions, CancellationToken))!.Id;
    }

    private async Task DeactivateAsync(int productId)
    {
        var response = await _client.PutAsJsonAsync($"/products/{productId}",
            new { name = "Inactive product", minStock = 10m, isActive = false }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task ReceiveAsync(int productId, decimal quantity) => RecordAsync(productId, StockMovementType.Receipt, quantity);

    private Task IssueAsync(int productId, decimal quantity) => RecordAsync(productId, StockMovementType.Issue, quantity);

    private async Task RecordAsync(int productId, StockMovementType type, decimal quantity)
    {
        var response = await _client.PostAsJsonAsync("/stock/movements",
            new { productId, type, quantity }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<int> CreateWorkOrderAsync(int productId, decimal quantity)
    {
        var response = await _client.PostAsJsonAsync("/work-orders", new { productId, quantity }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdOnly>(ApiFactory.JsonOptions, CancellationToken))!.Id;
    }

    private async Task<int> CreateOrderAsync(int customerId, int productId, DateOnly orderDate, DateOnly dueDate)
    {
        var response = await _client.PostAsJsonAsync("/sales-orders",
            new { customerId, orderDate, dueDate, lines = new[] { new { productId, quantity = 1m } } }, ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdOnly>(ApiFactory.JsonOptions, CancellationToken))!.Id;
    }

    private sealed record IdOnly(int Id);

    private sealed record DashboardResponse(
        ProductCountsResponse Products,
        IReadOnlyList<BelowMinimumResponse> BelowMinimum,
        IReadOnlyList<MovementResponse> RecentMovements,
        IReadOnlyList<ActivityDayResponse> Activity,
        OrderStatsResponse Orders,
        ProductionStatsResponse Production);

    private sealed record OrderStatsResponse(int Open, int Late, IReadOnlyList<DueOrderResponse> DueSoon);

    private sealed record DueOrderResponse(int Id, string Number, bool IsLate);

    private sealed record ProductionStatsResponse(int Planned, int InProgress, int DoneLast7Days, int ShortCount, IReadOnlyList<QueueResponse> Queue);

    private sealed record QueueResponse(int Id, string ProductCode, bool HasShortage);

    private sealed record ProductCountsResponse(int RawMaterial, int SemiFinished, int Finished, int Inactive);

    private sealed record BelowMinimumResponse(
        int ProductId, string Code, string Name, UnitOfMeasure Unit, decimal OnHand, decimal Available, decimal MinStock);

    private sealed record MovementResponse(int Id, string ProductCode, StockMovementType Type, decimal Quantity);

    private sealed record ActivityDayResponse(DateOnly Date, int In, int Out);
}
