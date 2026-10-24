using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Stock;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Dashboard;

/// <summary>Read-only summary of products and stock for the dashboard.</summary>
public sealed class DashboardService(IAppDbContext db, TimeProvider timeProvider)
{
    public const int RecentMovementCount = 8;
    public const int ActivityDays = 14;

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
            await GetActivityAsync(cancellationToken));
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

    /// <remarks>
    /// Compares on-hand with the minimum. Phase 4 defines critical stock on <em>available</em> stock
    /// (on-hand minus work order reservations), which needs the work orders of Phase 3.
    /// </remarks>
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
            })
            .Where(p => p.OnHand < p.MinStock)
            // Most critical first: the smallest share of the minimum still on hand.
            .OrderBy(p => p.OnHand / p.MinStock)
            .ThenBy(p => p.Code)
            .Select(p => new BelowMinimumDto(p.Id, p.Code, p.Name, p.Unit, p.OnHand, p.MinStock))
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
