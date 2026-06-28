
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObservabilityLab.Shared.Entities;

namespace ObservabilityLab.Shared.Database.Configurations
{
    internal class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("products");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(p => p.Price)
                .HasColumnName("price")
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.StockQuantity)
                .HasColumnName("stock_quantity")
                .IsRequired();

            builder.Property(p => p.IsInStock)
                .HasColumnName("is_in_stock")
                .IsRequired();

            builder.ToTable(t => t.HasCheckConstraint("CK_Product_StockQuantity", "stock_quantity >= 0"));
            builder.ToTable(t => t.HasCheckConstraint("CK_IsInStock_StockQuantity", "(is_in_stock = true AND stock_quantity > 0) OR (is_in_stock = false AND stock_quantity = 0)"));

        }
    }
}
