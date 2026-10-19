using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderInventory.Domain.Products;

namespace OrderInventory.Infrastructure.Persistence.Configurations;

internal sealed class BomLineConfiguration : IEntityTypeConfiguration<BomLine>
{
    public void Configure(EntityTypeBuilder<BomLine> builder)
    {
        // Last line of defence; the application validates these rules with clear messages first.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_bom_lines_qty_per_unit_positive", "qty_per_unit > 0");
            t.HasCheckConstraint("ck_bom_lines_not_self", "parent_product_id <> component_product_id");
        });

        builder.HasOne(l => l.ParentProduct)
            .WithMany(p => p.BomLines)
            .HasForeignKey(l => l.ParentProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.ComponentProduct)
            .WithMany()
            .HasForeignKey(l => l.ComponentProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.ParentProductId, l.ComponentProductId }).IsUnique();
    }
}
