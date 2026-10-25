using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Application.Stock;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Sales;
using OrderInventory.Domain.Stock;

namespace OrderInventory.Application.Production;

public sealed class WorkOrderService(IAppDbContext db, StockLedger ledger, TimeProvider timeProvider)
{
    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    public async Task<PagedResult<WorkOrderSummaryDto>> ListAsync(WorkOrderListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var workOrders = db.WorkOrders.AsNoTracking();
        if (query.Status is { } status)
        {
            workOrders = workOrders.Where(w => w.Status == status);
        }

        if (query.Open is { } open)
        {
            workOrders = open
                ? workOrders.Where(w => w.Status == WorkOrderStatus.Planned || w.Status == WorkOrderStatus.InProgress)
                : workOrders.Where(w => w.Status == WorkOrderStatus.Done || w.Status == WorkOrderStatus.Cancelled);
        }

        if (query.SalesOrderId is { } salesOrderId)
        {
            workOrders = workOrders.Where(w => w.SalesOrderLine!.SalesOrderId == salesOrderId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search);
            workOrders = workOrders.Where(w => EF.Functions.ILike(w.Number, pattern, LikePattern.Escape)
                || EF.Functions.ILike(w.Product!.Code, pattern, LikePattern.Escape)
                || EF.Functions.ILike(w.Product!.Name, pattern, LikePattern.Escape));
        }

        var totalCount = await workOrders.CountAsync(cancellationToken);
        var page = await workOrders
            // Open work first, then by plan date: the list reads like a production schedule.
            .OrderBy(w => w.Status == WorkOrderStatus.Done || w.Status == WorkOrderStatus.Cancelled)
            .ThenBy(w => w.PlannedDate)
            .ThenBy(w => w.Number)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(w => w.Product)
            .Include(w => w.Components)
            .Include(w => w.SalesOrderLine!.SalesOrder!.Customer)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var availability = await LoadAvailabilityAsync(page, cancellationToken);
        var items = page
            .Select(w => new WorkOrderSummaryDto(
                w.Id, w.Number, w.ProductId, w.Product!.Code, w.Product.Name, w.Product.Unit, w.Quantity, w.Status,
                w.PlannedDate, w.CompletedAt, w.SalesOrderLine?.SalesOrderId, w.SalesOrderLine?.SalesOrder?.Number,
                w.SalesOrderLine?.SalesOrder?.Customer?.Name,
                availability[w.Id].Any(c => c.Shortage > 0)))
            .ToList();

        return new PagedResult<WorkOrderSummaryDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<WorkOrderDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var workOrder = await db.WorkOrders.AsNoTracking()
            .Include(w => w.Product)
            .Include(w => w.Components).ThenInclude(c => c.ComponentProduct)
            .Include(w => w.SalesOrderLine!.SalesOrder!.Customer)
            .AsSplitQuery()
            .SingleOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw NotFound(id);

        var availability = (await LoadAvailabilityAsync([workOrder], cancellationToken))[workOrder.Id]
            .ToDictionary(a => a.ComponentProductId);

        var components = workOrder.Components
            .OrderBy(c => c.ComponentProduct!.Code, StringComparer.Ordinal)
            .Select(c =>
            {
                var a = availability[c.ComponentProductId];
                var p = c.ComponentProduct!;
                return new WorkOrderComponentDto(
                    c.ComponentProductId, p.Code, p.Name, p.Type, p.Unit, c.RequiredQty, c.ConsumedQty,
                    a.OnHand, a.Reserved, a.Available, a.Shortage);
            })
            .ToList();

        var line = workOrder.SalesOrderLine;
        return new WorkOrderDto(
            workOrder.Id, workOrder.Number, workOrder.ProductId, workOrder.Product!.Code, workOrder.Product.Name,
            workOrder.Product.Unit, workOrder.Quantity, workOrder.Status, workOrder.PlannedDate, workOrder.CompletedAt,
            workOrder.SalesOrderLineId, line?.SalesOrderId, line?.SalesOrder?.Number, line?.SalesOrder?.Customer?.Name,
            components.Any(c => c.Shortage > 0), components);
    }

    /// <summary>Rule 8: snapshots the product's direct BOM, scaled by the quantity.</summary>
    public async Task<WorkOrderDto> CreateAsync(CreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new BusinessRuleException($"Product {request.ProductId} does not exist.");
        if (product.Type == ProductType.RawMaterial)
        {
            throw new BusinessRuleException($"{product.Code} is a raw material; only semi-finished and finished products are produced.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (request.SalesOrderLineId is { } lineId)
        {
            // FOR SHARE on the order row: a concurrent cancel (which takes FOR UPDATE on the same row) waits
            // until this work order is committed and then cancels it too, or this request waits for the cancel
            // and then sees the order as Cancelled. Without it a work order could appear on a cancelled order.
            await db.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM sales_orders WHERE id = (SELECT sales_order_id FROM sales_order_lines WHERE id = {lineId}) FOR SHARE",
                cancellationToken);
            var orderLine = await db.SalesOrderLines.AsNoTracking()
                .Include(l => l.SalesOrder)
                .Include(l => l.Product)
                .SingleOrDefaultAsync(l => l.Id == lineId, cancellationToken)
                ?? throw new BusinessRuleException($"Sales order line {lineId} does not exist.");

            if (orderLine.ProductId != product.Id)
            {
                throw new BusinessRuleException(
                    $"Sales order line {lineId} is for {orderLine.Product!.Code}, not {product.Code}.");
            }

            if (orderLine.SalesOrder!.Status != SalesOrderStatus.Open)
            {
                throw new BusinessRuleException(
                    $"Sales order {orderLine.SalesOrder.Number} is {orderLine.SalesOrder.Status}; work orders can only be created for open orders.");
            }
        }

        // Read inside the transaction, so the snapshot is the BOM as it is when the work order is numbered.
        var bom = await db.BomLines.AsNoTracking()
            .Where(l => l.ParentProductId == product.Id)
            .Select(l => new { l.ComponentProductId, l.QtyPerUnit })
            .ToListAsync(cancellationToken);
        if (bom.Count == 0)
        {
            throw new BusinessRuleException($"{product.Code} has no bill of materials; add its components first.");
        }

        var workOrder = new WorkOrder
        {
            Number = await DocumentNumbers.NextAsync(
                db, DocumentNumbers.WorkOrderPrefix, db.WorkOrders.Select(w => w.Number), cancellationToken),
            ProductId = product.Id,
            Quantity = request.Quantity,
            SalesOrderLineId = request.SalesOrderLineId,
            PlannedDate = request.PlannedDate ?? Today,
        };
        foreach (var line in bom)
        {
            workOrder.Components.Add(new WorkOrderComponent
            {
                ComponentProductId = line.ComponentProductId,
                RequiredQty = line.QtyPerUnit * request.Quantity,
            });
        }

        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(workOrder.Id, cancellationToken);
    }

    public Task<WorkOrderDto> StartAsync(int id, CancellationToken cancellationToken) =>
        TransitionAsync(id, WorkOrderStatus.InProgress, "started", cancellationToken);

    /// <summary>Rule 11: nothing was consumed, so cancelling only releases the reservation.</summary>
    public Task<WorkOrderDto> CancelAsync(int id, CancellationToken cancellationToken) =>
        TransitionAsync(id, WorkOrderStatus.Cancelled, "cancelled", cancellationToken);

    /// <summary>
    /// Rule 10: InProgress → Done in one transaction. Consumes every component and outputs the product
    /// through the ledger, which locks the product rows and rejects the whole operation if any component is
    /// physically short (rule 3). Reserved shortage (rule 9) is a planning signal and does not block this.
    /// </summary>
    public async Task<WorkOrderDto> CompleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var workOrder = await LoadLockedAsync(id, WorkOrderStatus.Done, "completed", cancellationToken);

        var movements = workOrder.Components
            .Where(c => c.RequiredQty > c.ConsumedQty)
            .Select(c => new NewStockMovement(
                c.ComponentProductId, -(c.RequiredQty - c.ConsumedQty), StockMovementType.ProductionConsumption,
                StockMovementRefType.WorkOrder, workOrder.Id))
            .Append(new NewStockMovement(
                workOrder.ProductId, workOrder.Quantity, StockMovementType.ProductionOutput,
                StockMovementRefType.WorkOrder, workOrder.Id))
            .ToList();

        foreach (var component in workOrder.Components)
        {
            component.ConsumedQty = component.RequiredQty;
        }

        workOrder.Status = WorkOrderStatus.Done;
        workOrder.CompletedAt = timeProvider.GetUtcNow().UtcDateTime;

        // The ledger joins this transaction and its SaveChanges also saves the changes above. A shortage
        // throws before anything is written; disposing the transaction rolls back and releases the locks.
        await ledger.PostAsync(movements, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<WorkOrderDto> TransitionAsync(int id, WorkOrderStatus to, string action, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var workOrder = await LoadLockedAsync(id, to, action, cancellationToken);

        workOrder.Status = to;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Locks the work order row, then loads it and checks the transition (rule 12). Two requests on the same
    /// work order are serialised here, so the second sees the first one's status (a double click on Complete
    /// consumes once). Document rows are always locked before product rows, so this cannot deadlock with the ledger.
    /// </summary>
    private async Task<WorkOrder> LoadLockedAsync(int id, WorkOrderStatus to, string action, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM work_orders WHERE id = {id} FOR UPDATE", cancellationToken);
        var workOrder = await db.WorkOrders.Include(w => w.Components).SingleOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw NotFound(id);

        if (!WorkOrderTransitions.IsAllowed(workOrder.Status, to))
        {
            throw new ConflictException($"Work order {workOrder.Number} is {workOrder.Status}; it cannot be {action}.");
        }

        return workOrder;
    }

    /// <summary>
    /// Rule 9 for each of <paramref name="workOrders"/> (which must include their components). Closed work orders
    /// reserve nothing and report no shortage.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, IReadOnlyList<ComponentAvailability>>> LoadAvailabilityAsync(
        IReadOnlyCollection<WorkOrder> workOrders, CancellationToken cancellationToken)
    {
        var productIds = workOrders.SelectMany(w => w.Components).Select(c => c.ComponentProductId).Distinct().ToList();

        var onHand = await db.StockMovements.AsNoTracking()
            .Where(m => productIds.Contains(m.ProductId))
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, OnHand = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.OnHand, cancellationToken);

        // Only the components that matter here; Evaluate skips the work order being evaluated itself.
        var openWorkOrders = await db.WorkOrders.AsNoTracking()
            .Where(w => w.Status == WorkOrderStatus.Planned || w.Status == WorkOrderStatus.InProgress)
            .Include(w => w.Components.Where(c => productIds.Contains(c.ComponentProductId)))
            .ToListAsync(cancellationToken);

        return workOrders.ToDictionary(
            w => w.Id,
            w => WorkOrderTransitions.IsOpen(w.Status)
                ? WorkOrderAvailability.Evaluate(w, onHand, openWorkOrders)
                : ClosedAvailability(w, onHand));
    }

    private static IReadOnlyList<ComponentAvailability> ClosedAvailability(WorkOrder workOrder, IReadOnlyDictionary<int, decimal> onHand) =>
        workOrder.Components
            .OrderBy(c => c.ComponentProductId)
            .Select(c =>
            {
                var stock = onHand.GetValueOrDefault(c.ComponentProductId);
                return new ComponentAvailability(c.ComponentProductId, c.RequiredQty, stock, 0m, stock, 0m);
            })
            .ToList();

    private static NotFoundException NotFound(int id) =>
        new(string.Create(CultureInfo.InvariantCulture, $"Work order {id} was not found."));
}
