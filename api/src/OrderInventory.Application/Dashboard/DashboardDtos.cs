using OrderInventory.Application.Production;
using OrderInventory.Application.Stock;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Dashboard;

/// <summary>Everything the dashboard shows, in one request.</summary>
public sealed record DashboardDto(
    ProductCountsDto Products,
    IReadOnlyList<BelowMinimumDto> BelowMinimum,
    IReadOnlyList<StockMovementDto> RecentMovements,
    IReadOnlyList<ActivityDayDto> Activity,
    OrderStatsDto Orders,
    ProductionStatsDto Production);

/// <summary>Active products per type, and how many are inactive.</summary>
public sealed record ProductCountsDto(int RawMaterial, int SemiFinished, int Finished, int Inactive);

/// <summary>An active product whose available stock (on-hand minus open work order reservations) is below its minimum.</summary>
public sealed record BelowMinimumDto(
    int ProductId, string Code, string Name, UnitOfMeasure Unit, decimal OnHand, decimal Available, decimal MinStock);

/// <summary>Number of movements that added (<see cref="In"/>) or removed (<see cref="Out"/>) stock on one UTC day.</summary>
public sealed record ActivityDayDto(DateOnly Date, int In, int Out);

/// <summary>Open sales orders, how many are late (rule 15), and the next ones due, late first.</summary>
public sealed record OrderStatsDto(int Open, int Late, IReadOnlyList<DueOrderDto> DueSoon);

public sealed record DueOrderDto(int Id, string Number, string CustomerName, DateOnly DueDate, bool IsLate);

/// <summary>
/// Work orders per open status, completions in the last seven days, how many open ones are short
/// (rule 9, reserved shortage) and the production queue: open work orders in plan order.
/// </summary>
public sealed record ProductionStatsDto(
    int Planned, int InProgress, int DoneLast7Days, int ShortCount, IReadOnlyList<WorkOrderSummaryDto> Queue);
