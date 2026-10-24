using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Sales;
using OrderInventory.Domain.Stock;
using OrderInventory.Domain.Users;

namespace OrderInventory.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderComponent> WorkOrderComponents => Set<WorkOrderComponent>();
    public DbSet<User> Users => Set<User>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureLedgerIsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureLedgerIsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Rule 2: stock movements are only ever added. Init-only properties already stop most changes at
    /// compile time; this also catches Remove() and changes made through the change tracker.
    /// (Bulk ExecuteUpdate/ExecuteDelete bypass SaveChanges; nothing in the application uses them on the ledger.)
    /// </summary>
    private void EnsureLedgerIsAppendOnly()
    {
        var changed = ChangeTracker.Entries<StockMovement>()
            .FirstOrDefault(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (changed is not null)
        {
            throw new InvalidOperationException(
                $"Stock movement {changed.Entity.Id} cannot be {(changed.State == EntityState.Deleted ? "deleted" : "changed")}: " +
                "the ledger is append-only. Record an Adjustment instead.");
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All quantities share one precision: numeric(18,4).
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);

        // Enums are stored by name so the data stays readable and reordering members is safe.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
