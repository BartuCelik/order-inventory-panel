using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderInventory.Domain.Products;

namespace OrderInventory.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("ck_products_min_stock_non_negative", "min_stock >= 0"));

        builder.Property(p => p.Code).HasMaxLength(32);
        builder.Property(p => p.Name).HasMaxLength(200);

        builder.HasIndex(p => p.Code).IsUnique();
    }
}
