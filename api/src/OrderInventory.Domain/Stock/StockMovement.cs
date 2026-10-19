using OrderInventory.Domain.Common;
using OrderInventory.Domain.Products;

namespace OrderInventory.Domain.Stock;

/// <summary>
/// One entry in the append-only stock ledger. Properties are init-only:
/// a movement is never changed after it is written; mistakes are fixed with an
/// <see cref="StockMovementType.Adjustment"/>.
/// </summary>
public sealed class StockMovement : Entity
{
    public int ProductId { get; init; }
    public Product? Product { get; init; }

    /// <summary>Signed quantity: positive adds stock, negative removes it.</summary>
    public decimal Quantity { get; init; }

    public StockMovementType Type { get; init; }
    public StockMovementRefType? RefType { get; init; }
    public int? RefId { get; init; }
    public string? Note { get; init; }

    /// <summary>Who wrote the movement; always taken from the authenticated user.</summary>
    public required string CreatedBy { get; init; }

    public DateTime CreatedAt { get; init; }
}
