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
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(existingLines);

        if (parent.Type == ProductType.RawMaterial)
        {
            return BomLineViolation.ParentIsRawMaterial;
        }

        if (qtyPerUnit <= 0)
        {
            return BomLineViolation.QtyPerUnitNotPositive;
        }

        // Materialised once: the duplicate check and the cycle check both walk the lines.
        var lines = existingLines as IReadOnlyCollection<BomLine> ?? existingLines.ToList();

        if (lines.Any(l => l.ParentProductId == parent.Id && l.ComponentProductId == componentProductId))
        {
            return BomLineViolation.DuplicateComponent;
        }

        if (BomCycleCheck.WouldCreateCycle(parent.Id, componentProductId, lines))
        {
            return BomLineViolation.WouldCreateCycle;
        }

        return null;
    }
}
