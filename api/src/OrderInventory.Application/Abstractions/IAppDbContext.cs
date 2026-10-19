using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Sales;
using OrderInventory.Domain.Stock;
using OrderInventory.Domain.Users;

namespace OrderInventory.Application.Abstractions;

/// <summary>
/// The application's view of the database. Services use EF Core directly through this
/// interface instead of a repository layer.
/// </summary>
public interface IAppDbContext
{
    DbSet<Product> Products { get; }
    DbSet<BomLine> BomLines { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<Customer> Customers { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderLine> SalesOrderLines { get; }
    DbSet<WorkOrder> WorkOrders { get; }
    DbSet<WorkOrderComponent> WorkOrderComponents { get; }
    DbSet<User> Users { get; }

    /// <summary>Needed for explicit transactions and row locks on stock-changing operations.</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
