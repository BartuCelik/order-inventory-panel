using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderInventory.Domain.Production;

namespace OrderInventory.Infrastructure.Persistence.Configurations;

internal sealed class WorkOrderComponentConfiguration : IEntityTypeConfiguration<WorkOrderComponent>
{
    public void Configure(EntityTypeBuilder<WorkOrderComponent> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_work_order_components_required_qty_positive", "required_qty > 0");
            t.HasCheckConstraint(
                "ck_work_order_components_consumed_qty_range",
                "consumed_qty >= 0 AND consumed_qty <= required_qty");
        });

        builder.HasOne(c => c.ComponentProduct)
            .WithMany()
            .HasForeignKey(c => c.ComponentProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.WorkOrderId, c.ComponentProductId }).IsUnique();
    }
}
