namespace OrderInventory.Domain.Stock;

/// <summary>A product whose on-hand stock would drop below zero (rule 3).</summary>
/// <param name="Requested">The net quantity the operation takes out of stock (positive).</param>
public sealed record StockShortage(int ProductId, decimal OnHand, decimal Requested)
{
    public decimal Missing => Requested - OnHand;
}

public static class StockRules
{
    /// <summary>
    /// Returns every product whose on-hand would become negative if all <paramref name="movements"/> were
    /// written together, ordered by product id; empty when the operation may go ahead. Movements of the
    /// same product are netted first. A product missing from <paramref name="onHand"/> has nothing on hand.
    /// </summary>
    public static IReadOnlyList<StockShortage> FindShortages(
        IReadOnlyDictionary<int, decimal> onHand, IEnumerable<StockMovement> movements)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Whether <paramref name="quantity"/> has the sign <paramref name="type"/> requires:
    /// Receipt and ProductionOutput add (+), Issue and ProductionConsumption remove (−), Adjustment is either. Never zero.
    /// </summary>
    public static bool HasValidSign(StockMovementType type, decimal quantity)
    {
        throw new NotImplementedException();
    }
}
