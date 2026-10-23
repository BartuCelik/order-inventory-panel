namespace OrderInventory.Domain.Products;

/// <summary>
/// Validates a new bill of materials line against rules 5 and 6: raw materials have no BOM,
/// <c>QtyPerUnit &gt; 0</c>, no duplicate (parent, component) pair and no cycles.
/// </summary>
public static class BomLineRules
{
    /// <summary>
    /// Returns the first rule that adding <paramref name="componentProductId"/> × <paramref name="qtyPerUnit"/>
    /// to <paramref name="parent"/> would break, or <c>null</c> when the line may be added.
    /// <paramref name="existingLines"/> must hold every BOM line, not only the parent's, so indirect cycles are seen.
    /// </summary>
    public static BomLineViolation? CheckNewLine(
        Product parent, int componentProductId, decimal qtyPerUnit, IEnumerable<BomLine> existingLines)
    {
        throw new NotImplementedException();
    }
}
