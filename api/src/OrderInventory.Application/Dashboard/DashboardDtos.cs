using OrderInventory.Application.Stock;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Dashboard;

/// <summary>Everything the dashboard shows, in one request.</summary>
public sealed record DashboardDto(
    ProductCountsDto Products,
    IReadOnlyList<BelowMinimumDto> BelowMinimum,
    IReadOnlyList<StockMovementDto> RecentMovements,
    IReadOnlyList<ActivityDayDto> Activity);

/// <summary>Active products per type, and how many are inactive.</summary>
public sealed record ProductCountsDto(int RawMaterial, int SemiFinished, int Finished, int Inactive);

/// <summary>An active product whose on-hand stock is below its minimum.</summary>
public sealed record BelowMinimumDto(int ProductId, string Code, string Name, UnitOfMeasure Unit, decimal OnHand, decimal MinStock);

/// <summary>Number of movements that added (<see cref="In"/>) or removed (<see cref="Out"/>) stock on one UTC day.</summary>
public sealed record ActivityDayDto(DateOnly Date, int In, int Out);
