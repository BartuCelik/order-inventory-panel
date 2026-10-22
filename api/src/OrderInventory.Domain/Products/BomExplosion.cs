namespace OrderInventory.Domain.Products;

/// <summary>
/// Multi-level bill of materials explosion (rule 7): the total raw-material need for making
/// a quantity of a product, recursing through its semi-finished components.
/// </summary>
public static class BomExplosion
{
    /// <summary>
    /// Returns the total need per leaf component (product id → quantity) for <paramref name="quantity"/>
    /// units of <paramref name="productId"/>. A leaf is a component that has no BOM lines of its own.
    /// </summary>
    public static IReadOnlyDictionary<int, decimal> Explode(int productId, decimal quantity, IEnumerable<BomLine> bomLines)
    {
        throw new NotImplementedException();
    }
}
