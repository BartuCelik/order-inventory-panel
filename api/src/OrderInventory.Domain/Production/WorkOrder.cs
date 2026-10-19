using OrderInventory.Domain.Common;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Sales;

namespace OrderInventory.Domain.Production;

public sealed class WorkOrder : Entity
{
    public required string Number { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>The sales order line this work order was created for, if any.</summary>
    public int? SalesOrderLineId { get; set; }
    public SalesOrderLine? SalesOrderLine { get; set; }

    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Planned;
    public DateOnly PlannedDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Snapshot of the product's direct BOM lines taken when the work order was created.</summary>
    public ICollection<WorkOrderComponent> Components { get; } = [];
}
