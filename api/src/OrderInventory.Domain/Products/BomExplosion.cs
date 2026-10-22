using System.Globalization;

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
        ArgumentNullException.ThrowIfNull(bomLines);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                string.Format(CultureInfo.InvariantCulture, "Quantity must be greater than zero, was {0}.", quantity));
        }

        var linesByParent = bomLines.ToLookup(l => l.ParentProductId);
        var need = new Dictionary<int, decimal>();

        // Only the ancestors of the current node, not every node seen so far: a shared subassembly
        // reached through two branches is valid and must be counted on each path.
        var path = new HashSet<int>();
        Visit(productId, quantity, linesByParent, path, need);
        return need;
    }

    private static void Visit(
        int productId, decimal multiplier, ILookup<int, BomLine> linesByParent, HashSet<int> path, Dictionary<int, decimal> need)
    {
        path.Add(productId);

        foreach (var line in linesByParent[productId])
        {
            var componentId = line.ComponentProductId;
            var componentNeed = multiplier * line.QtyPerUnit;

            if (path.Contains(componentId))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The bill of materials contains a cycle: product {0} is a component of itself (via product {1}).",
                    componentId,
                    productId));
            }

            if (linesByParent.Contains(componentId))
            {
                Visit(componentId, componentNeed, linesByParent, path, need);
            }
            else
            {
                need[componentId] = need.GetValueOrDefault(componentId) + componentNeed;
            }
        }

        path.Remove(productId);
    }
}
