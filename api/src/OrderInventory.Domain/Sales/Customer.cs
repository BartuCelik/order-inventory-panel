using OrderInventory.Domain.Common;

namespace OrderInventory.Domain.Sales;

public sealed class Customer : Entity
{
    public required string Name { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
