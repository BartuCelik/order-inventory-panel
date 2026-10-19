using OrderInventory.Domain.Common;

namespace OrderInventory.Domain.Sales;

public sealed class SalesOrder : Entity
{
    public required string Number { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateOnly OrderDate { get; set; }
    public DateOnly DueDate { get; set; }
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Open;
    public DateTime? ShippedAt { get; set; }

    public ICollection<SalesOrderLine> Lines { get; } = [];
}
