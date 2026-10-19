using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderInventory.Domain.Stock;

namespace OrderInventory.Infrastructure.Persistence.Configurations;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("ck_stock_movements_quantity_non_zero", "quantity <> 0"));

        builder.Property(m => m.Note).HasMaxLength(500);
        builder.Property(m => m.CreatedBy).HasMaxLength(256);

        builder.HasOne(m => m.Product)
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // On-hand is SUM(quantity) per product; this index lets that aggregate run index-only.
        builder.HasIndex(m => new { m.ProductId, m.Quantity });
        builder.HasIndex(m => new { m.RefType, m.RefId });
    }
}
