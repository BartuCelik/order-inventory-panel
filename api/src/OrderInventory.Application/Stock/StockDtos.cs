using System.ComponentModel.DataAnnotations;
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
/// <remarks>
/// Quantities are bounded by the <c>numeric(18,4)</c> column: below 0.0001 the database would round to zero,
/// above 14 integer digits it cannot store the value.
/// </remarks>
public sealed record RecordStockMovementRequest(
    int ProductId,
    StockMovementType Type,
    decimal Quantity,
    [StringLength(500)] string? Note = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var magnitude = Math.Abs(Quantity);
        var inRange = magnitude is >= MinQuantity and <= MaxQuantity;

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

    private const decimal MinQuantity = 0.0001m;
    private const decimal MaxQuantity = 99_999_999_999_999m;

    /// <summary>The signed quantity stored in the ledger.</summary>
    public decimal SignedQuantity => Type == StockMovementType.Issue ? -Quantity : Quantity;
}

public sealed record StockMovementQuery
{
    [Range(1, 100_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public int? ProductId { get; init; }
}
