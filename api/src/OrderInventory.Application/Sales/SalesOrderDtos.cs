using System.ComponentModel.DataAnnotations;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Sales;

namespace OrderInventory.Application.Sales;

/// <summary>A row of the sales order list. <see cref="IsLate"/> is derived, never stored (rule 15).</summary>
public sealed record SalesOrderSummaryDto(
    int Id, string Number, int CustomerId, string CustomerName, DateOnly OrderDate, DateOnly DueDate,
    SalesOrderStatus Status, DateTime? ShippedAt, bool IsLate);

public sealed record SalesOrderDto(
    int Id, string Number, int CustomerId, string CustomerName, DateOnly OrderDate, DateOnly DueDate,
    SalesOrderStatus Status, DateTime? ShippedAt, bool IsLate, IReadOnlyList<SalesOrderLineDto> Lines);

/// <summary>A line with the finished good's current on-hand, so the user sees whether it can ship (rule 14).</summary>
public sealed record SalesOrderLineDto(
    int Id, int ProductId, string ProductCode, string ProductName, UnitOfMeasure Unit, decimal Quantity, decimal OnHand,
    IReadOnlyList<LineWorkOrderDto> WorkOrders);

/// <summary>A work order created for a sales order line (rule 13).</summary>
public sealed record LineWorkOrderDto(int Id, string Number, decimal Quantity, WorkOrderStatus Status);

public sealed record CreateSalesOrderRequest(
    int CustomerId,
    DateOnly? OrderDate,
    DateOnly DueDate,
    [MinLength(1, ErrorMessage = "An order needs at least one line.")] IReadOnlyList<SalesOrderLineRequest> Lines) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OrderDate is { } orderDate && DueDate < orderDate)
        {
            yield return new ValidationResult("The due date cannot be before the order date.", [nameof(DueDate)]);
        }

        // One line per product keeps shipping and work order links unambiguous.
        if (Lines.GroupBy(l => l.ProductId).Any(g => g.Count() > 1))
        {
            yield return new ValidationResult("Each product can appear on only one line.", [nameof(Lines)]);
        }
    }
}

/// <remarks>Quantity is bounded by the <c>numeric(18,4)</c> column, as in main's product requests.</remarks>
public sealed record SalesOrderLineRequest(
    int ProductId,
    [Range(typeof(decimal), "0.0001", "99999999999999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    decimal Quantity);

public sealed record SalesOrderListQuery
{
    [Range(1, 100_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public SalesOrderStatus? Status { get; init; }

    /// <summary>true: only late orders (Open and past due).</summary>
    public bool? Late { get; init; }

    /// <summary>Case-insensitive match in the order number or the customer name.</summary>
    [StringLength(100)]
    public string? Search { get; init; }
}
