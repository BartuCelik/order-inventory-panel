namespace OrderInventory.Domain.Stock;

public enum StockMovementType
{
    /// <summary>Goods received (+).</summary>
    Receipt,

    /// <summary>Goods issued, e.g. shipped to a customer (−).</summary>
    Issue,

    /// <summary>Manual correction after a count or a mistake (±).</summary>
    Adjustment,

    /// <summary>Components consumed by a completed work order (−).</summary>
    ProductionConsumption,

    /// <summary>Products produced by a completed work order (+).</summary>
    ProductionOutput,
}
