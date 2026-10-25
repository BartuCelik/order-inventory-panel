using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Production;
using OrderInventory.Application.Stock;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Sales;

namespace OrderInventory.Application.Dashboard;

/// <summary>Read-only summary of products and stock for the dashboard.</summary>
public sealed class DashboardService(IAppDbContext db, WorkOrderService workOrders, TimeProvider timeProvider)
{
    public const int RecentMovementCount = 8;
    public const int ActivityDays = 14;
    public const int DueSoonCount = 6;
    public const int QueueCount = 6;
    public const int WeeksShown = 8;

    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        return new DashboardDto(
            await CountProductsAsync(cancellationToken),
            await FindBelowMinimumAsync(cancellationToken),
            await db.StockMovements.AsNoTracking()
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Take(RecentMovementCount)
                .Select(StockService.ToDto)
                .ToListAsync(cancellationToken),
            await GetActivityAsync(cancellationToken),
            await GetOrderStatsAsync(cancellationToken),
            await GetProductionStatsAsync(cancellationToken));
    }

    private async Task<OrderStatsDto> GetOrderStatsAsync(CancellationToken cancellationToken)
    {
        var today = Today;
        var open = db.SalesOrders.AsNoTracking().Where(o => o.Status == SalesOrderStatus.Open);

        return new OrderStatsDto(
            await open.CountAsync(cancellationToken),
            await open.CountAsync(o => o.DueDate < today, cancellationToken),
            // Ordered by due date, so late orders (due before today) come first.
            await open
                .OrderBy(o => o.DueDate)
                .ThenBy(o => o.Number)
                .Take(DueSoonCount)
                .Select(o => new DueOrderDto(o.Id, o.Number, o.Customer!.Name, o.DueDate, o.DueDate < today))
                .ToListAsync(cancellationToken));
    }

    private async Task<ProductionStatsDto> GetProductionStatsAsync(CancellationToken cancellationToken)
    {
        var since = timeProvider.GetUtcNow().UtcDateTime.AddDays(-7);
        var counts = await db.WorkOrders.AsNoTracking()
            .GroupBy(w => w.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        var doneLast7Days = await db.WorkOrders.AsNoTracking()
            .CountAsync(w => w.Status == WorkOrderStatus.Done && w.CompletedAt >= since, cancellationToken);

        // The shortage flag needs rule 9 per work order. 100 open work orders is far more than a small
        // workshop runs at once; the queue is the first few of them in plan order.
        var open = await workOrders.ListAsync(new WorkOrderListQuery { Open = true, PageSize = 100 }, cancellationToken);

        return new ProductionStatsDto(
            counts.GetValueOrDefault(WorkOrderStatus.Planned),
            counts.GetValueOrDefault(WorkOrderStatus.InProgress),
            doneLast7Days,
            open.Items.Count(w => w.HasShortage),
            open.Items.Take(QueueCount).ToList(),
            await GetWeeklyProductionAsync(cancellationToken));
    }

    private async Task<IReadOnlyList<ProductionWeekDto>> GetWeeklyProductionAsync(CancellationToken cancellationToken)
    {
        var today = Today;
        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var first = thisMonday.AddDays(-7 * (WeeksShown - 1));
        var from = first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // A few weeks of a small workshop's completions: grouping them in memory is simpler than week maths in SQL.
        var completed = await db.WorkOrders.AsNoTracking()
            .Where(w => w.Status == WorkOrderStatus.Done && w.CompletedAt >= from)
            .Select(w => w.CompletedAt!.Value)
            .ToListAsync(cancellationToken);
        var perWeek = completed
            .GroupBy(at => (DateOnly.FromDateTime(at).DayNumber - first.DayNumber) / 7)
            .ToDictionary(g => g.Key, g => g.Count());

        // Every week of the window, including the quiet ones, so the chart has no gaps.
        return Enumerable.Range(0, WeeksShown)
            .Select(i => new ProductionWeekDto(first.AddDays(7 * i), perWeek.GetValueOrDefault(i)))
            .ToList();
    }

    private async Task<ProductCountsDto> CountProductsAsync(CancellationToken cancellationToken)
    {
        var counts = await db.Products.AsNoTracking()
            .GroupBy(p => new { p.IsActive, p.Type })
            .Select(g => new { g.Key.IsActive, g.Key.Type, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Active(ProductType type) => counts.Where(c => c.IsActive && c.Type == type).Sum(c => c.Count);

        return new ProductCountsDto(
            Active(ProductType.RawMaterial),
            Active(ProductType.SemiFinished),
            Active(ProductType.Finished),
            counts.Where(c => !c.IsActive).Sum(c => c.Count));
    }

    /// <summary>
    /// Critical stock: available (on-hand minus what open work orders still reserve, rule 9) below the minimum.
    /// On-hand alone would hide material that is already promised to planned production.
    /// </summary>
    private async Task<IReadOnlyList<BelowMinimumDto>> FindBelowMinimumAsync(CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.MinStock > 0)
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.Name,
                p.Unit,
                p.MinStock,
                // Rule 1: on-hand is always the sum of the ledger.
                OnHand = db.StockMovements.Where(m => m.ProductId == p.Id).Sum(m => m.Quantity),
                Reserved = db.WorkOrderComponents
                    .Where(c => c.ComponentProductId == p.Id
                        && (c.WorkOrder!.Status == WorkOrderStatus.Planned || c.WorkOrder.Status == WorkOrderStatus.InProgress))
                    .Sum(c => c.RequiredQty - c.ConsumedQty),
            })
            .Select(p => new { p.Id, p.Code, p.Name, p.Unit, p.MinStock, p.OnHand, Available = p.OnHand - p.Reserved })
            .Where(p => p.Available < p.MinStock)
            // Most critical first: the smallest share of the minimum still available.
            .OrderBy(p => p.Available / p.MinStock)
            .ThenBy(p => p.Code)
            .Select(p => new BelowMinimumDto(p.Id, p.Code, p.Name, p.Unit, p.OnHand, p.Available, p.MinStock))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<ActivityDayDto>> GetActivityAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var first = today.AddDays(-(ActivityDays - 1));
        var from = first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var perDay = await db.StockMovements.AsNoTracking()
            .Where(m => m.CreatedAt >= from)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new { Day = g.Key, In = g.Count(m => m.Quantity > 0), Out = g.Count(m => m.Quantity < 0) })
            .ToDictionaryAsync(d => DateOnly.FromDateTime(d.Day), cancellationToken);

        // Every day of the window, including the quiet ones, so the chart has no gaps.
        return Enumerable.Range(0, ActivityDays)
            .Select(i => first.AddDays(i))
            .Select(day => perDay.TryGetValue(day, out var d) ? new ActivityDayDto(day, d.In, d.Out) : new ActivityDayDto(day, 0, 0))
            .ToList();
    }
}
