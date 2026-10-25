using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Sales;
using OrderInventory.Domain.Stock;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Sales;

public sealed class SalesOrdersApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    // The tests in this class share one database, so every test uses its own code prefix.
    private readonly HttpClient _client = api.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // ---------- create ----------

    [Fact]
    public async Task Post_Valid_Returns201WithNumberLinesAndOnHand()
    {
        var customer = await _client.CreateCustomerAsync("SO-Create Customer");
        var table = await _client.CreateProductAsync("SOC-TABLE", ProductType.Finished);
        await _client.ReceiveAsync(table, 3m);

        var response = await PostOrderAsync(customer, Today.AddDays(10), (table, 2m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.ReadAsync<OrderResponse>();
        Assert.Equal($"/sales-orders/{order.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Matches(@"^SO-\d{5}$", order.Number);
        Assert.Equal("SO-Create Customer", order.CustomerName);
        Assert.Equal(Today, order.OrderDate);
        Assert.Equal(SalesOrderStatus.Open, order.Status);
        Assert.False(order.IsLate);
        var line = Assert.Single(order.Lines);
        Assert.Equal(("SOC-TABLE", 2m, 3m), (line.ProductCode, line.Quantity, line.OnHand));
    }

    [Fact]
    public async Task Post_NumbersAreSequential()
    {
        var customer = await _client.CreateCustomerAsync("SO-Number Customer");
        var chair = await _client.CreateProductAsync("SON-CHAIR", ProductType.Finished);

        var first = await (await PostOrderAsync(customer, Today, (chair, 1m))).ReadAsync<OrderResponse>();
        var second = await (await PostOrderAsync(customer, Today, (chair, 1m))).ReadAsync<OrderResponse>();

        Assert.Equal(Sequence(first.Number) + 1, Sequence(second.Number));
    }

    [Theory]
    [InlineData(ProductType.RawMaterial)]
    [InlineData(ProductType.SemiFinished)]
    public async Task Post_LineForNonFinishedProduct_Returns422(ProductType type)
    {
        // Rule 13: only finished goods are sold.
        var customer = await _client.CreateCustomerAsync($"SO-Rule13 {type}");
        var product = await _client.CreateProductAsync($"SO13-{type}", type);

        var response = await PostOrderAsync(customer, Today, (product, 1m));

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, $"SO13-{type}");
    }

    [Fact]
    public async Task Post_UnknownCustomerOrProduct_Returns422()
    {
        var customer = await _client.CreateCustomerAsync("SO-Unknown Customer");
        var bench = await _client.CreateProductAsync("SOU-BENCH", ProductType.Finished);

        await (await PostOrderAsync(999999, Today, (bench, 1m))).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "999999");
        await (await PostOrderAsync(customer, Today, (999998, 1m))).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "999998");
    }

    [Fact]
    public async Task Post_InvalidRequests_Return400()
    {
        var customer = await _client.CreateCustomerAsync("SO-400 Customer");
        var desk = await _client.CreateProductAsync("SO4-DESK", ProductType.Finished);

        // No lines, a non-positive quantity, the same product twice, due before order date.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostOrderAsync(customer, Today)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostOrderAsync(customer, Today, (desk, 0m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostOrderAsync(customer, Today, (desk, 1m), (desk, 2m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostOrderAsync(customer, Today, Today.AddDays(-1), (desk, 1m))).StatusCode);
    }

    // ---------- list / late (rule 15) ----------

    [Fact]
    public async Task List_FlagsLateOrdersAndFiltersByLate()
    {
        var customer = await _client.CreateCustomerAsync("SO-Late Customer");
        var stool = await _client.CreateProductAsync("SOL-STOOL", ProductType.Finished);
        await _client.ReceiveAsync(stool, 5m);
        // Past-due orders were placed earlier; an order cannot be due before it was placed.
        var placed = Today.AddDays(-10);
        var late = await CreateOrderAsync(customer, placed, Today.AddDays(-1), (stool, 1m));
        var dueToday = await CreateOrderAsync(customer, placed, Today, (stool, 1m));
        var shippedPastDue = await CreateOrderAsync(customer, placed, Today.AddDays(-2), (stool, 1m));
        Assert.Equal(HttpStatusCode.OK, (await ShipAsync(shippedPastDue.Id)).StatusCode);

        var all = await ListAsync("search=SO-Late Customer");
        var lateOnly = await ListAsync("search=SO-Late Customer&late=true");

        // Late = Open and DueDate before today; due today is not late, shipped is never late.
        Assert.Equal(
            new Dictionary<int, bool> { [late.Id] = true, [dueToday.Id] = false, [shippedPastDue.Id] = false },
            all.Items.ToDictionary(o => o.Id, o => o.IsLate));
        Assert.Equal([late.Id], lateOnly.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task List_FiltersByStatusAndSearchesNumber()
    {
        var customer = await _client.CreateCustomerAsync("SO-Filter Customer");
        var shelf = await _client.CreateProductAsync("SOF-SHELF", ProductType.Finished);
        var open = await CreateOrderAsync(customer, Today, (shelf, 1m));
        var cancelled = await CreateOrderAsync(customer, Today, (shelf, 1m));
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(cancelled.Id)).StatusCode);

        var cancelledOnly = await ListAsync("search=SO-Filter Customer&status=Cancelled");
        var byNumber = await ListAsync($"search={open.Number}");

        Assert.Equal([cancelled.Id], cancelledOnly.Items.Select(o => o.Id));
        Assert.Equal([open.Id], byNumber.Items.Select(o => o.Id));
    }

    // ---------- ship (rule 14) ----------

    [Fact]
    public async Task Ship_IssuesEveryLineAndMarksShipped()
    {
        var customer = await _client.CreateCustomerAsync("SO-Ship Customer");
        var table = await _client.CreateProductAsync("SOS-TABLE", ProductType.Finished);
        var chair = await _client.CreateProductAsync("SOS-CHAIR", ProductType.Finished);
        await _client.ReceiveAsync(table, 2m);
        await _client.ReceiveAsync(chair, 10m);
        var order = await CreateOrderAsync(customer, Today, (table, 1m), (chair, 4m));

        var response = await ShipAsync(order.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shipped = await response.ReadAsync<OrderResponse>();
        Assert.Equal(SalesOrderStatus.Shipped, shipped.Status);
        Assert.NotNull(shipped.ShippedAt);
        Assert.Equal(1m, await _client.OnHandAsync(table));
        Assert.Equal(6m, await _client.OnHandAsync(chair));

        await using var scope = api.CreateScope();
        var movements = await ApiFactory.GetDbContext(scope).StockMovements
            .Where(m => m.RefType == StockMovementRefType.SalesOrder && m.RefId == order.Id)
            .ToListAsync(CancellationToken);
        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.Equal(StockMovementType.Issue, m.Type));
    }

    [Fact]
    public async Task Ship_WithAShortLine_Returns422AndChangesNothing()
    {
        var customer = await _client.CreateCustomerAsync("SO-Short Customer");
        var table = await _client.CreateProductAsync("SOX-TABLE", ProductType.Finished);
        var chair = await _client.CreateProductAsync("SOX-CHAIR", ProductType.Finished);
        await _client.ReceiveAsync(table, 5m);
        await _client.ReceiveAsync(chair, 1m);
        var order = await CreateOrderAsync(customer, Today, (table, 1m), (chair, 4m));

        var response = await ShipAsync(order.Id);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "SOX-CHAIR");
        Assert.Equal(5m, await _client.OnHandAsync(table));
        Assert.Equal(1m, await _client.OnHandAsync(chair));
        Assert.Equal(SalesOrderStatus.Open, (await GetAsync(order.Id)).Status);
    }

    [Theory]
    [InlineData("ship")]
    [InlineData("cancel")]
    public async Task ShipOrCancel_WhenNotOpen_Returns409(string action)
    {
        var customer = await _client.CreateCustomerAsync($"SO-409 {action}");
        var lamp = await _client.CreateProductAsync($"SO9-{action}", ProductType.Finished);
        var order = await CreateOrderAsync(customer, Today, (lamp, 1m));
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(order.Id)).StatusCode);

        var response = await _client.PostAsync($"/sales-orders/{order.Id}/{action}", null, CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, "Cancelled");
    }

    [Fact]
    public async Task Ship_UnknownOrder_Returns404()
    {
        await (await ShipAsync(999999)).AssertProblemAsync(HttpStatusCode.NotFound, "999999");
    }

    [Fact]
    public async Task Ship_TwiceAtTheSameMoment_IssuesStockOnce()
    {
        // Both requests would see the order as Open; locking the order row makes the second see it shipped.
        var customer = await _client.CreateCustomerAsync("SO-Race Customer");
        var bench = await _client.CreateProductAsync("SOR-BENCH", ProductType.Finished);
        await _client.ReceiveAsync(bench, 10m);

        for (var i = 0; i < 5; i++)
        {
            var order = await CreateOrderAsync(customer, Today, (bench, 1m));

            var responses = await Task.WhenAll(ShipAsync(order.Id), ShipAsync(order.Id));

            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
        }
        Assert.Equal(5m, await _client.OnHandAsync(bench));
    }

    // ---------- helpers ----------

    private Task<HttpResponseMessage> PostOrderAsync(int customerId, DateOnly dueDate, params (int ProductId, decimal Quantity)[] lines) =>
        PostOrderAsync(customerId, null, dueDate, lines);

    private Task<HttpResponseMessage> PostOrderAsync(
        int customerId, DateOnly? orderDate, DateOnly dueDate, params (int ProductId, decimal Quantity)[] lines) =>
        _client.PostAsJsonAsync("/sales-orders",
            new
            {
                customerId,
                orderDate,
                dueDate,
                lines = lines.Select(l => new { productId = l.ProductId, quantity = l.Quantity }),
            },
            ApiFactory.JsonOptions, CancellationToken);

    private Task<OrderResponse> CreateOrderAsync(int customerId, DateOnly dueDate, params (int ProductId, decimal Quantity)[] lines) =>
        CreateOrderAsync(customerId, null, dueDate, lines);

    private async Task<OrderResponse> CreateOrderAsync(
        int customerId, DateOnly? orderDate, DateOnly dueDate, params (int ProductId, decimal Quantity)[] lines)
    {
        var response = await PostOrderAsync(customerId, orderDate, dueDate, lines);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<OrderResponse>();
    }

    private static int Sequence(string number) => int.Parse(number[3..], System.Globalization.CultureInfo.InvariantCulture);

    private Task<HttpResponseMessage> ShipAsync(int id) => _client.PostAsync($"/sales-orders/{id}/ship", null, CancellationToken);

    private Task<HttpResponseMessage> CancelAsync(int id) => _client.PostAsync($"/sales-orders/{id}/cancel", null, CancellationToken);

    private async Task<OrderResponse> GetAsync(int id) =>
        (await _client.GetFromJsonAsync<OrderResponse>($"/sales-orders/{id}", ApiFactory.JsonOptions, CancellationToken))!;

    private async Task<PagedResponse<OrderSummaryResponse>> ListAsync(string query)
    {
        var response = await _client.GetAsync($"/sales-orders?{query}", CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResponse<OrderSummaryResponse>>();
    }

    private sealed record OrderResponse(
        int Id, string Number, int CustomerId, string CustomerName, DateOnly OrderDate, DateOnly DueDate,
        SalesOrderStatus Status, DateTime? ShippedAt, bool IsLate, IReadOnlyList<LineResponse> Lines);

    private sealed record LineResponse(
        int Id, int ProductId, string ProductCode, string ProductName, UnitOfMeasure Unit, decimal Quantity, decimal OnHand);

    private sealed record OrderSummaryResponse(
        int Id, string Number, int CustomerId, string CustomerName, DateOnly OrderDate, DateOnly DueDate,
        SalesOrderStatus Status, DateTime? ShippedAt, bool IsLate);

    private sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
}
