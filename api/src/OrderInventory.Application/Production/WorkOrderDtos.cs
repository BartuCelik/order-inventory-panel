using System.ComponentModel.DataAnnotations;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;

namespace OrderInventory.Application.Production;

/// <summary>
/// A row of the work order list. <see cref="HasShortage"/> is the rule 9 planning signal (reserved shortage);
/// it is always false for closed work orders.
/// </summary>
public sealed record WorkOrderSummaryDto(
    int Id, string Number, int ProductId, string ProductCode, string ProductName, UnitOfMeasure Unit, decimal Quantity,
    WorkOrderStatus Status, DateOnly PlannedDate, DateTime? CompletedAt,
    int? SalesOrderId, string? SalesOrderNumber, string? CustomerName, bool HasShortage);

public sealed record WorkOrderDto(
    int Id, string Number, int ProductId, string ProductCode, string ProductName, UnitOfMeasure Unit, decimal Quantity,
    WorkOrderStatus Status, DateOnly PlannedDate, DateTime? CompletedAt,
    int? SalesOrderLineId, int? SalesOrderId, string? SalesOrderNumber, string? CustomerName,
    bool HasShortage, IReadOnlyList<WorkOrderComponentDto> Components);

/// <summary>
/// One snapshotted component with its availability (rule 9). For closed work orders nothing is reserved,
/// so <see cref="Reserved"/> and <see cref="Shortage"/> are zero and <see cref="Available"/> equals on-hand.
/// </summary>
public sealed record WorkOrderComponentDto(
    int ProductId, string ProductCode, string ProductName, ProductType Type, UnitOfMeasure Unit,
    decimal RequiredQty, decimal ConsumedQty, decimal OnHand, decimal Reserved, decimal Available, decimal Shortage);

public sealed record CreateWorkOrderRequest(
    int ProductId,
    [Range(typeof(decimal), "0.0001", "99999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal Quantity,
    DateOnly? PlannedDate,
    int? SalesOrderLineId);

public sealed record WorkOrderListQuery
{
    [Range(1, 100_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public WorkOrderStatus? Status { get; init; }

    /// <summary>true: only Planned and InProgress.</summary>
    public bool? Open { get; init; }

    public int? SalesOrderId { get; init; }

    /// <summary>Case-insensitive match in the work order number or the product code or name.</summary>
    [StringLength(100)]
    public string? Search { get; init; }
}
