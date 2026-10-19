using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderInventory.Domain.Production;

namespace OrderInventory.Infrastructure.Persistence.Configurations;

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("ck_work_orders_quantity_positive", "quantity > 0"));

        builder.Property(w => w.Number).HasMaxLength(32);
        builder.HasIndex(w => w.Number).IsUnique();

        builder.HasOne(w => w.Product)
            .WithMany()
            .HasForeignKey(w => w.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.SalesOrderLine)
            .WithMany()
            .HasForeignKey(w => w.SalesOrderLineId)
            .OnDelete(DeleteBehavior.Restrict);

        // The component snapshot belongs to the work order.
        builder.HasMany(w => w.Components)
            .WithOne(c => c.WorkOrder)
            .HasForeignKey(c => c.WorkOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Reservation only counts open work orders (Planned / InProgress).
        builder.HasIndex(w => w.Status);
    }
}
