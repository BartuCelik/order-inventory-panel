using OrderInventory.Domain.Common;

namespace OrderInventory.Domain.Products;

/// <summary>
/// One line of a bill of materials: <see cref="QtyPerUnit"/> of the component
/// is needed to make one unit of the parent product.
/// </summary>
public sealed class BomLine : Entity
{
    public int ParentProductId { get; set; }
    public Product? ParentProduct { get; set; }

    public int ComponentProductId { get; set; }
    public Product? ComponentProduct { get; set; }

    public decimal QtyPerUnit { get; set; }
}
