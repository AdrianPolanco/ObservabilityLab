
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObservabilityLab.Shared.Entities;

namespace ObservabilityLab.Shared.Database.Configurations
{
    internal class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("order_items");

            builder.HasKey(oi => oi.Id);

            builder.Property(oi => oi.OrderId)
                .IsRequired()
                .HasColumnName("order_id");

            builder.Property(oi => oi.ProductId)
                .IsRequired()
                .HasColumnName("product_id");

            builder.Property(oi => oi.Quantity)
                .IsRequired()
                .HasColumnName("quantity");

            builder.Property(oi => oi.UnitPrice)
                .IsRequired()
                .HasColumnName("unit_price")
                .HasColumnType("decimal(18,2)");

            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
