using OrderInventory.Domain.Common;
using OrderInventory.Domain.Products;

namespace OrderInventory.Domain.Production;

public sealed class WorkOrderComponent : Entity
{
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int ComponentProductId { get; set; }
    public Product? ComponentProduct { get; set; }

    /// <summary>QtyPerUnit × work order quantity, fixed at creation.</summary>
    public decimal RequiredQty { get; set; }

    public decimal ConsumedQty { get; set; }
}
