namespace OrderInventory.Domain.Products;

/// <summary>A reason a new BOM line is rejected (rules 5 and 6).</summary>
public enum BomLineViolation
{
    ParentIsRawMaterial,
    QtyPerUnitNotPositive,
    DuplicateComponent,
    WouldCreateCycle,
}
