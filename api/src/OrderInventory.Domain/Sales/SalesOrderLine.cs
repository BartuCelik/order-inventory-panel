using OrderInventory.Domain.Common;
using OrderInventory.Domain.Products;

namespace OrderInventory.Domain.Sales;

public sealed class SalesOrderLine : Entity
{
    public int SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }

    /// <summary>Must reference a <see cref="ProductType.Finished"/> product.</summary>
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal Quantity { get; set; }
}
