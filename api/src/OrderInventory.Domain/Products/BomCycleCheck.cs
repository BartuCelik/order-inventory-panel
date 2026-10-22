namespace OrderInventory.Domain.Products;

/// <summary>
/// Guards rule 6: a bill of materials may never contain a cycle, including indirect ones (A→B→A).
/// </summary>
public static class BomCycleCheck
{
    /// <summary>
    /// Returns <c>true</c> when adding the line <paramref name="parentProductId"/> → <paramref name="componentProductId"/>
    /// to <paramref name="existingLines"/> would create a cycle.
    /// </summary>
    public static bool WouldCreateCycle(int parentProductId, int componentProductId, IEnumerable<BomLine> existingLines)
    {
        throw new NotImplementedException();
    }
}
