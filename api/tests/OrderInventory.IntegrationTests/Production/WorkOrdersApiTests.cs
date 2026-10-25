using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Stock;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Production;

public sealed class WorkOrdersApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    // The tests in this class share one database, so every test uses its own code prefix.
    private readonly HttpClient _client = api.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // ---------- create (rule 8) ----------

    [Fact]
    public async Task Post_SnapshotsTheDirectBomScaledByQuantity()
    {
        // TABLE ← 4 × LEG (semi-finished) + 1 × TOP; LEG ← 0.5 × BEAM. Only the direct lines are snapshotted.
        var (table, leg, top) = await CreateTableAsync("WOC");
        var beam = await _client.CreateProductAsync("WOC-BEAM", ProductType.RawMaterial, UnitOfMeasure.Meter);
        await _client.AddBomLineAsync(leg, beam, 0.5m);

        var response = await PostAsync(new { productId = table, quantity = 3m, plannedDate = Today.AddDays(2) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var workOrder = await response.ReadAsync<WorkOrderResponse>();
        Assert.Equal($"/work-orders/{workOrder.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Matches(@"^WO-\d{5}$", workOrder.Number);
        Assert.Equal((WorkOrderStatus.Planned, 3m, Today.AddDays(2)), (workOrder.Status, workOrder.Quantity, workOrder.PlannedDate));
        Assert.Equal(
            [("WOC-LEG", 12m, 0m), ("WOC-TOP", 3m, 0m)],
            workOrder.Components.Select(c => (c.ProductCode, c.RequiredQty, c.ConsumedQty)).OrderBy(c => c.ProductCode));
    }

    [Fact]
    public async Task Post_LaterBomEditsDoNotChangeAnExistingWorkOrder()
    {
        var (table, leg, _) = await CreateTableAsync("WOS");
        var workOrder = await CreateAsync(table, 2m);
        var glue = await _client.CreateProductAsync("WOS-GLUE", ProductType.RawMaterial, UnitOfMeasure.Liter);

        await _client.AddBomLineAsync(table, glue, 0.1m);
        var legLine = await GetBomLineIdAsync(table, leg);
        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.DeleteAsync($"/products/{table}/bom-lines/{legLine}", CancellationToken)).StatusCode);

        var reloaded = await GetAsync(workOrder.Id);
        Assert.Equal(["WOS-LEG", "WOS-TOP"], reloaded.Components.Select(c => c.ProductCode).Order());
    }

    [Fact]
    public async Task Post_FromASalesOrderLine_LinksTheOrder()
    {
        var (table, _, _) = await CreateTableAsync("WOL");
        var customer = await _client.CreateCustomerAsync("WO-Link Customer");
        var (orderId, lineId) = await CreateSalesOrderAsync(customer, table, 2m);

        var workOrder = await CreateAsync(table, 2m, salesOrderLineId: lineId);

        Assert.Equal(lineId, workOrder.SalesOrderLineId);
        Assert.Equal(orderId, workOrder.SalesOrderId);
        Assert.Matches(@"^SO-\d{5}$", workOrder.SalesOrderNumber);
    }

    [Fact]
    public async Task Post_InvalidSalesOrderLine_Returns422()
    {
        var (table, _, _) = await CreateTableAsync("WOX");
        var (chair, _, _) = await CreateTableAsync("WOX2");
        var customer = await _client.CreateCustomerAsync("WO-BadLine Customer");
        var (_, chairLine) = await CreateSalesOrderAsync(customer, chair, 1m);
        var (cancelledOrder, tableLine) = await CreateSalesOrderAsync(customer, table, 1m);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsync($"/sales-orders/{cancelledOrder}/cancel", null, CancellationToken)).StatusCode);

        // Unknown line, a line for another product, a line on an order that is no longer open.
        await (await PostAsync(new { productId = table, quantity = 1m, salesOrderLineId = 999999 }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "999999");
        await (await PostAsync(new { productId = table, quantity = 1m, salesOrderLineId = chairLine }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "WOX2-TABLE");
        await (await PostAsync(new { productId = table, quantity = 1m, salesOrderLineId = tableLine }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "Cancelled");
    }

    [Fact]
    public async Task Post_ProductThatCannotBeProduced_Returns422()
    {
        var raw = await _client.CreateProductAsync("WOP-RAW", ProductType.RawMaterial);
        var noBom = await _client.CreateProductAsync("WOP-NOBOM", ProductType.Finished);

        await (await PostAsync(new { productId = raw, quantity = 1m })).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "WOP-RAW");
        await (await PostAsync(new { productId = noBom, quantity = 1m })).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "WOP-NOBOM");
        await (await PostAsync(new { productId = 999997, quantity = 1m })).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "999997");
    }

    [Fact]
    public async Task Post_NonPositiveQuantity_Returns400()
    {
        var (table, _, _) = await CreateTableAsync("WO4");

        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(new { productId = table, quantity = 0m })).StatusCode);
    }

    // ---------- availability (rule 9) ----------

    [Fact]
    public async Task Get_ShowsAvailabilityWithReservationByOtherOpenWorkOrders()
    {
        // 10 legs on hand. A needs 8 (2 tables × 4), B needs 4: B sees 10 − 8 = 2 available, 2 short.
        var (table, leg, top) = await CreateTableAsync("WOA");
        await _client.ReceiveAsync(leg, 10m);
        await _client.ReceiveAsync(top, 5m);
        var a = await CreateAsync(table, 2m);
        var b = await CreateAsync(table, 1m);
        var cancelled = await CreateAsync(table, 5m);
        Assert.Equal(HttpStatusCode.OK, (await ActAsync(cancelled.Id, "cancel")).StatusCode);

        var detail = await GetAsync(b.Id);

        var legs = detail.Components.Single(c => c.ProductCode == "WOA-LEG");
        Assert.Equal((4m, 10m, 8m, 2m, 2m), (legs.RequiredQty, legs.OnHand, legs.Reserved, legs.Available, legs.Shortage));
        Assert.True(detail.HasShortage);
        Assert.False((await GetAsync(a.Id)).HasShortage);
    }

    [Fact]
    public async Task List_FiltersByStatusAndFlagsShortages()
    {
        var (table, _, _) = await CreateTableAsync("WOF");
        var planned = await CreateAsync(table, 1m);
        var started = await CreateAsync(table, 1m);
        Assert.Equal(HttpStatusCode.OK, (await ActAsync(started.Id, "start")).StatusCode);

        var inProgress = await ListAsync("search=WOF-TABLE&status=InProgress");
        var all = await ListAsync("search=WOF-TABLE");

        Assert.Equal([started.Id], inProgress.Items.Select(w => w.Id));
        Assert.Equal([planned.Id, started.Id], all.Items.Select(w => w.Id).Order());
        // Nothing on hand: both are short.
        Assert.All(all.Items, w => Assert.True(w.HasShortage));
    }

    // ---------- transitions (rules 11, 12) ----------

    [Fact]
    public async Task StartAndCancel_FollowTheTransitionRules()
    {
        var (table, _, _) = await CreateTableAsync("WOT");
        var workOrder = await CreateAsync(table, 1m);

        var started = await (await ActAsync(workOrder.Id, "start")).ReadAsync<WorkOrderResponse>();
        Assert.Equal(WorkOrderStatus.InProgress, started.Status);
        await (await ActAsync(workOrder.Id, "start")).AssertProblemAsync(HttpStatusCode.Conflict, "InProgress");

        var cancelled = await (await ActAsync(workOrder.Id, "cancel")).ReadAsync<WorkOrderResponse>();
        Assert.Equal(WorkOrderStatus.Cancelled, cancelled.Status);
        await (await ActAsync(workOrder.Id, "cancel")).AssertProblemAsync(HttpStatusCode.Conflict, "Cancelled");
        await (await ActAsync(workOrder.Id, "start")).AssertProblemAsync(HttpStatusCode.Conflict, "Cancelled");
    }

    [Fact]
    public async Task Complete_FromPlanned_Returns409()
    {
        var (table, _, _) = await CreateTableAsync("WOP2");
        var workOrder = await CreateAsync(table, 1m);

        await (await ActAsync(workOrder.Id, "complete")).AssertProblemAsync(HttpStatusCode.Conflict, "Planned");
    }

    [Fact]
    public async Task Actions_UnknownWorkOrder_Return404()
    {
        await (await ActAsync(999999, "start")).AssertProblemAsync(HttpStatusCode.NotFound, "999999");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/work-orders/999999", CancellationToken)).StatusCode);
    }

    // ---------- complete (rule 10) ----------

    [Fact]
    public async Task Complete_ConsumesComponentsAndOutputsTheProduct()
    {
        var (table, leg, top) = await CreateTableAsync("WOD");
        await _client.ReceiveAsync(leg, 10m);
        await _client.ReceiveAsync(top, 3m);
        var workOrder = await CreateAsync(table, 2m);
        Assert.Equal(HttpStatusCode.OK, (await ActAsync(workOrder.Id, "start")).StatusCode);

        var response = await ActAsync(workOrder.Id, "complete");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var done = await response.ReadAsync<WorkOrderResponse>();
        Assert.Equal(WorkOrderStatus.Done, done.Status);
        Assert.NotNull(done.CompletedAt);
        Assert.All(done.Components, c => Assert.Equal(c.RequiredQty, c.ConsumedQty));
        Assert.Equal((2m, 1m, 2m), (await _client.OnHandAsync(leg), await _client.OnHandAsync(top), await _client.OnHandAsync(table)));

        await using var scope = api.CreateScope();
        var movements = await ApiFactory.GetDbContext(scope).StockMovements
            .Where(m => m.RefType == StockMovementRefType.WorkOrder && m.RefId == workOrder.Id)
            .Select(m => new { m.ProductId, m.Type, m.Quantity })
            .ToListAsync(CancellationToken);
        Assert.Equal(3, movements.Count);
        Assert.Contains(movements, m => m.ProductId == leg && m.Type == StockMovementType.ProductionConsumption && m.Quantity == -8m);
        Assert.Contains(movements, m => m.ProductId == table && m.Type == StockMovementType.ProductionOutput && m.Quantity == 2m);
    }

    [Fact]
    public async Task Complete_WithAShortComponent_Returns422AndWritesNothing()
    {
        var (table, leg, top) = await CreateTableAsync("WOK");
        await _client.ReceiveAsync(leg, 3m);
        await _client.ReceiveAsync(top, 5m);
        var workOrder = await CreateAsync(table, 1m);
        Assert.Equal(HttpStatusCode.OK, (await ActAsync(workOrder.Id, "start")).StatusCode);

        var response = await ActAsync(workOrder.Id, "complete");

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "WOK-LEG");
        Assert.Equal((3m, 5m, 0m), (await _client.OnHandAsync(leg), await _client.OnHandAsync(top), await _client.OnHandAsync(table)));
        var reloaded = await GetAsync(workOrder.Id);
        Assert.Equal(WorkOrderStatus.InProgress, reloaded.Status);
        Assert.All(reloaded.Components, c => Assert.Equal(0m, c.ConsumedQty));
    }

    [Fact]
    public async Task Complete_UsesPhysicalOnHandNotReservedAvailability()
    {
        // Rule 10: 10 legs on hand, A needs 4, B needs 8. Neither is "available" with reservations,
        // but A can still be made from what is physically there.
        var (table, leg, top) = await CreateTableAsync("WOR");
        await _client.ReceiveAsync(leg, 10m);
        await _client.ReceiveAsync(top, 10m);
        var a = await CreateAsync(table, 1m);
        await CreateAsync(table, 2m);
        Assert.True((await GetAsync(a.Id)).HasShortage);
        Assert.Equal(HttpStatusCode.OK, (await ActAsync(a.Id, "start")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await ActAsync(a.Id, "complete")).StatusCode);
        Assert.Equal(6m, await _client.OnHandAsync(leg));
    }

    [Fact]
    public async Task Complete_TwiceAtTheSameMoment_ConsumesOnce()
    {
        var (table, leg, top) = await CreateTableAsync("WO2X");
        await _client.ReceiveAsync(leg, 100m);
        await _client.ReceiveAsync(top, 100m);

        for (var i = 0; i < 5; i++)
        {
            var workOrder = await CreateAsync(table, 1m);
            Assert.Equal(HttpStatusCode.OK, (await ActAsync(workOrder.Id, "start")).StatusCode);

            var responses = await Task.WhenAll(ActAsync(workOrder.Id, "complete"), ActAsync(workOrder.Id, "complete"));

            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
        }

        Assert.Equal((80m, 95m, 5m), (await _client.OnHandAsync(leg), await _client.OnHandAsync(top), await _client.OnHandAsync(table)));
    }

    // ---------- sales order cancel cascades (owner decision 2026-10-25) ----------

    [Fact]
    public async Task CancellingASalesOrder_CancelsItsOpenWorkOrders()
    {
        var (table, leg, top) = await CreateTableAsync("WOO");
        await _client.ReceiveAsync(leg, 4m);
        await _client.ReceiveAsync(top, 1m);
        var customer = await _client.CreateCustomerAsync("WO-Cascade Customer");
        var (orderId, lineId) = await CreateSalesOrderAsync(customer, table, 3m);
        var planned = await CreateAsync(table, 1m, salesOrderLineId: lineId);
        var started = await CreateAsync(table, 1m, salesOrderLineId: lineId);
        var done = await CreateAsync(table, 1m, salesOrderLineId: lineId);
        var unrelated = await CreateAsync(table, 1m);
        Assert.Equal(HttpStatusCode.OK, (await ActAsync(started.Id, "start")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ActAsync(done.Id, "start")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ActAsync(done.Id, "complete")).StatusCode);

        var response = await _client.PostAsync($"/sales-orders/{orderId}/cancel", null, CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WorkOrderStatus.Cancelled, (await GetAsync(planned.Id)).Status);
        Assert.Equal(WorkOrderStatus.Cancelled, (await GetAsync(started.Id)).Status);
        // A finished work order produced real stock; it stays Done. Others are untouched.
        Assert.Equal(WorkOrderStatus.Done, (await GetAsync(done.Id)).Status);
        Assert.Equal(WorkOrderStatus.Planned, (await GetAsync(unrelated.Id)).Status);
    }

    // ---------- helpers ----------

    /// <summary>A finished TABLE made of 4 × LEG (semi-finished) and 1 × TOP (semi-finished).</summary>
    private async Task<(int Table, int Leg, int Top)> CreateTableAsync(string prefix)
    {
        var table = await _client.CreateProductAsync($"{prefix}-TABLE", ProductType.Finished);
        var leg = await _client.CreateProductAsync($"{prefix}-LEG", ProductType.SemiFinished);
        var top = await _client.CreateProductAsync($"{prefix}-TOP", ProductType.SemiFinished);
        await _client.AddBomLineAsync(table, leg, 4m);
        await _client.AddBomLineAsync(table, top, 1m);
        return (table, leg, top);
    }

    private async Task<(int OrderId, int LineId)> CreateSalesOrderAsync(int customerId, int productId, decimal quantity)
    {
        var response = await _client.PostAsJsonAsync("/sales-orders",
            new { customerId, dueDate = Today.AddDays(7), lines = new[] { new { productId, quantity } } },
            ApiFactory.JsonOptions, CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.ReadAsync<SalesOrderResponse>();
        return (order.Id, order.Lines.Single().Id);
    }

    private async Task<int> GetBomLineIdAsync(int parentId, int componentId)
    {
        var product = await _client.GetFromJsonAsync<ProductWithBom>($"/products/{parentId}", ApiFactory.JsonOptions, CancellationToken);
        return product!.BomLines.Single(l => l.ComponentProductId == componentId).Id;
    }

    private Task<HttpResponseMessage> PostAsync(object body) =>
        _client.PostAsJsonAsync("/work-orders", body, ApiFactory.JsonOptions, CancellationToken);

    private async Task<WorkOrderResponse> CreateAsync(int productId, decimal quantity, int? salesOrderLineId = null)
    {
        var response = await PostAsync(new { productId, quantity, salesOrderLineId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<WorkOrderResponse>();
    }

    private Task<HttpResponseMessage> ActAsync(int id, string action) =>
        _client.PostAsync($"/work-orders/{id}/{action}", null, CancellationToken);

    private async Task<WorkOrderResponse> GetAsync(int id) =>
        (await _client.GetFromJsonAsync<WorkOrderResponse>($"/work-orders/{id}", ApiFactory.JsonOptions, CancellationToken))!;

    private async Task<PagedResponse<WorkOrderSummaryResponse>> ListAsync(string query)
    {
        var response = await _client.GetAsync($"/work-orders?{query}", CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<PagedResponse<WorkOrderSummaryResponse>>();
    }

    private sealed record WorkOrderResponse(
        int Id, string Number, int ProductId, string ProductCode, decimal Quantity, WorkOrderStatus Status,
        DateOnly PlannedDate, DateTime? CompletedAt, int? SalesOrderLineId, int? SalesOrderId, string? SalesOrderNumber,
        bool HasShortage, IReadOnlyList<ComponentResponse> Components);

    private sealed record ComponentResponse(
        int ProductId, string ProductCode, decimal RequiredQty, decimal ConsumedQty,
        decimal OnHand, decimal Reserved, decimal Available, decimal Shortage);

    private sealed record WorkOrderSummaryResponse(int Id, string Number, WorkOrderStatus Status, bool HasShortage);

    private sealed record SalesOrderResponse(int Id, IReadOnlyList<SalesOrderLineResponse> Lines);

    private sealed record SalesOrderLineResponse(int Id);

    private sealed record ProductWithBom(IReadOnlyList<BomLineResponse> BomLines);

    private sealed record BomLineResponse(int Id, int ComponentProductId);

    private sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
}
