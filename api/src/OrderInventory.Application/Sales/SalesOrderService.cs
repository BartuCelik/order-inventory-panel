using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Application.Stock;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Sales;
using OrderInventory.Domain.Stock;

namespace OrderInventory.Application.Sales;

public sealed class SalesOrderService(IAppDbContext db, StockLedger ledger, TimeProvider timeProvider)
{
    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    /// <summary>Every row the list's filters match, for Excel export (see <see cref="Export"/>).</summary>
    public async Task<IReadOnlyList<SalesOrderSummaryDto>> ExportAsync(SalesOrderListQuery query, CancellationToken cancellationToken) =>
        Export.Rows(await ListAsync(query with { Page = 1, PageSize = Export.MaxRows }, cancellationToken));

    public async Task<PagedResult<SalesOrderSummaryDto>> ListAsync(SalesOrderListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var today = Today;

        var orders = db.SalesOrders.AsQueryable();
        if (query.Status is { } status)
        {
            orders = orders.Where(o => o.Status == status);
        }

        if (query.Late is { } late)
        {
            orders = late
                ? orders.Where(o => o.Status == SalesOrderStatus.Open && o.DueDate < today)
                : orders.Where(o => !(o.Status == SalesOrderStatus.Open && o.DueDate < today));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search);
            orders = orders.Where(o => EF.Functions.ILike(o.Number, pattern, LikePattern.Escape)
                || EF.Functions.ILike(o.Customer!.Name, pattern, LikePattern.Escape));
        }

        var totalCount = await orders.CountAsync(cancellationToken);
        var items = await orders
            .OrderBy(o => o.DueDate)
            .ThenBy(o => o.Number)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new SalesOrderSummaryDto(
                o.Id, o.Number, o.CustomerId, o.Customer!.Name, o.OrderDate, o.DueDate, o.Status, o.ShippedAt,
                o.Status == SalesOrderStatus.Open && o.DueDate < today))
            .ToListAsync(cancellationToken);

        return new PagedResult<SalesOrderSummaryDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<SalesOrderDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var today = Today;
        return await db.SalesOrders
            .Where(o => o.Id == id)
            .Select(o => new SalesOrderDto(
                o.Id, o.Number, o.CustomerId, o.Customer!.Name, o.OrderDate, o.DueDate, o.Status, o.ShippedAt,
                o.Status == SalesOrderStatus.Open && o.DueDate < today,
                o.Lines
                    .OrderBy(l => l.Product!.Code)
                    .Select(l => new SalesOrderLineDto(
                        l.Id, l.ProductId, l.Product!.Code, l.Product.Name, l.Product.Unit, l.Quantity,
                        db.StockMovements.Where(m => m.ProductId == l.ProductId).Sum(m => m.Quantity),
                        db.WorkOrders
                            .Where(w => w.SalesOrderLineId == l.Id)
                            .OrderBy(w => w.Number)
                            .Select(w => new LineWorkOrderDto(w.Id, w.Number, w.Quantity, w.Status))
                            .ToList()))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound(id);
    }

    public async Task<SalesOrderDto> CreateAsync(CreateSalesOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var orderDate = request.OrderDate ?? Today;
        if (request.DueDate < orderDate)
        {
            throw new BusinessRuleException("The due date cannot be before the order date.");
        }

        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new BusinessRuleException($"Customer {request.CustomerId} does not exist.");
        }

        var productIds = request.Lines.Select(l => l.ProductId).ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var missing = productIds.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessRuleException($"Product {string.Join(", ", missing)} does not exist.");
        }

        // Rule 13: only finished goods are sold.
        var notFinished = products.Values.Where(p => p.Type != ProductType.Finished).Select(p => p.Code).ToList();
        if (notFinished.Count > 0)
        {
            throw new BusinessRuleException(
                $"Only finished products can be sold; {string.Join(", ", notFinished)} {(notFinished.Count == 1 ? "is" : "are")} not finished.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = new SalesOrder
        {
            Number = await DocumentNumbers.NextAsync(
                db, DocumentNumbers.SalesOrderPrefix, db.SalesOrders.Select(o => o.Number), cancellationToken),
            CustomerId = request.CustomerId,
            OrderDate = orderDate,
            DueDate = request.DueDate,
        };
        foreach (var line in request.Lines)
        {
            order.Lines.Add(new SalesOrderLine { ProductId = line.ProductId, Quantity = line.Quantity });
        }

        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(order.Id, cancellationToken);
    }

    /// <summary>
    /// Rule 14: Open → Shipped in one transaction, with an Issue movement per line. Fails as a whole if
    /// any finished good is short.
    /// </summary>
    public async Task<SalesOrderDto> ShipAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LoadLockedOpenOrderAsync(id, "shipped", cancellationToken);

        order.Status = SalesOrderStatus.Shipped;
        order.ShippedAt = timeProvider.GetUtcNow().UtcDateTime;

        // The ledger joins this transaction: it locks the product rows, checks stock, and its
        // SaveChanges also saves the status change above. A shortage throws and nothing is committed.
        await ledger.PostAsync(
            order.Lines
                .Select(l => new NewStockMovement(
                    l.ProductId, -l.Quantity, StockMovementType.Issue, StockMovementRefType.SalesOrder, order.Id))
                .ToList(),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Open → Cancelled. The order's open work orders (Planned/InProgress) are cancelled with it, which releases
    /// their reservations (owner decision 2026-10-25). Done work orders produced real stock and stay Done.
    /// </summary>
    public async Task<SalesOrderDto> CancelAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = await LoadLockedOpenOrderAsync(id, "cancelled", cancellationToken);

        // Lock order: sales order row (above), then its work order rows in id order. Completing a work order
        // locks the work order row, then product rows, so the two never wait on each other in a cycle.
        // If a complete holds a row here, FOR UPDATE waits for it and then re-checks the status, so a work
        // order that just became Done is skipped rather than cancelled.
        await db.Database.ExecuteSqlAsync(
            $"""
            SELECT 1 FROM work_orders
            WHERE sales_order_line_id IN (SELECT id FROM sales_order_lines WHERE sales_order_id = {id})
              AND status IN ('Planned', 'InProgress')
            ORDER BY id
            FOR UPDATE
            """,
            cancellationToken);
        var openWorkOrders = await db.WorkOrders
            .Where(w => w.SalesOrderLine!.SalesOrderId == id
                && (w.Status == WorkOrderStatus.Planned || w.Status == WorkOrderStatus.InProgress))
            .ToListAsync(cancellationToken);
        foreach (var workOrder in openWorkOrders)
        {
            workOrder.Status = WorkOrderStatus.Cancelled;
        }

        order.Status = SalesOrderStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Locks the order row, then loads it. Two requests on the same order are serialised here, so the
    /// second one sees the first one's status (e.g. a double click on Ship issues stock once).
    /// Document rows are always locked before product rows, so this cannot deadlock with the ledger.
    /// </summary>
    private async Task<SalesOrder> LoadLockedOpenOrderAsync(int id, string action, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM sales_orders WHERE id = {id} FOR UPDATE", cancellationToken);
        var order = await db.SalesOrders.Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw NotFound(id);

        if (order.Status != SalesOrderStatus.Open)
        {
            throw new ConflictException($"Sales order {order.Number} is {order.Status}; only open orders can be {action}.");
        }

        return order;
    }

    private static NotFoundException NotFound(int id) => new($"Sales order {id} was not found.");
}
