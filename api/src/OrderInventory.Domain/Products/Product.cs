using OrderInventory.Domain.Common;

namespace OrderInventory.Domain.Products;

/// <summary>
/// Anything the workshop buys, makes or sells. On-hand stock is never stored here;
/// it is always the sum of the product's stock movements.
/// </summary>
public sealed class Product : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public ProductType Type { get; set; }
    public UnitOfMeasure Unit { get; set; }
    public decimal MinStock { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Components this product is made of (empty for raw materials).</summary>
    public ICollection<BomLine> BomLines { get; } = [];
}
