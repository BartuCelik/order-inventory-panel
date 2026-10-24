using OrderInventory.Domain.Stock;

namespace OrderInventory.Application.Stock;

/// <summary>A movement to write; the ledger adds who and when.</summary>
public sealed record NewStockMovement(
    int ProductId,
    decimal Quantity,
    StockMovementType Type,
    StockMovementRefType? RefType = null,
    int? RefId = null,
    string? Note = null);

/// <summary>
/// The only way stock changes (rules 2–4). Every caller — manual movements now, work order completion and
/// shipping later — writes through <see cref="PostAsync"/>.
/// </summary>
public sealed class StockLedger
{
    public Task<IReadOnlyList<StockMovement>> PostAsync(IReadOnlyCollection<NewStockMovement> movements, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
