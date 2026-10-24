using System.ComponentModel.DataAnnotations;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Stock;

namespace OrderInventory.Application.Stock;

public sealed record StockMovementDto(
    int Id,
    int ProductId,
    string ProductCode,
    string ProductName,
    UnitOfMeasure Unit,
    StockMovementType Type,
    decimal Quantity,
    StockMovementRefType? RefType,
    int? RefId,
    string? Note,
    string CreatedBy,
    DateTime CreatedAt);

/// <summary>
/// A movement entered by hand. Receipt and Issue take a positive quantity and the type decides the
/// direction; Adjustment is signed. There is deliberately no CreatedBy: it comes from the current user.
/// </summary>
public sealed record RecordStockMovementRequest(
    int ProductId,
    StockMovementType Type,
    decimal Quantity,
    [StringLength(500)] string? Note = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var magnitude = Math.Abs(Quantity);
        var inRange = magnitude >= (decimal)QuantityLimits.MinPositive && magnitude <= (decimal)QuantityLimits.Max;

        switch (Type)
        {
            case StockMovementType.Receipt or StockMovementType.Issue when Quantity <= 0 || !inRange:
                yield return new ValidationResult(
                    "Enter a positive quantity; the movement type decides whether stock goes up or down.", [nameof(Quantity)]);
                break;
            case StockMovementType.Adjustment when !inRange:
                yield return new ValidationResult(
                    "Enter a non-zero quantity: positive adds stock, negative removes it.", [nameof(Quantity)]);
                break;
            case StockMovementType.ProductionConsumption or StockMovementType.ProductionOutput:
                yield return new ValidationResult(
                    "Production movements are written when a work order is completed, not by hand.", [nameof(Type)]);
                break;
        }
    }

    /// <summary>The signed quantity stored in the ledger.</summary>
    public decimal SignedQuantity => Type == StockMovementType.Issue ? -Quantity : Quantity;
}

public sealed class StockMovementQuery : PageQuery
{
    public int? ProductId { get; init; }
}
