namespace OrderInventory.Domain.Stock;

/// <summary>The kind of document that caused a stock movement.</summary>
public enum StockMovementRefType
{
    SalesOrder,
    WorkOrder,
}
