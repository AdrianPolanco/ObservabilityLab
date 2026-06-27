

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObservabilityLab.Shared.Entities;

namespace ObservabilityLab.Shared.Database.Configurations
{
    internal class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("orders");

            builder.HasKey(o => o.Id);

            builder.Property(o => o.CustomerId)
                .IsRequired()
                .HasColumnName("customer_id");

            builder.Property(o => o.Status)
                .IsRequired()
                .HasColumnName("status")
                .HasConversion<string>();

            builder.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey("OrderId")
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(o => o.CreatedAt)
                .IsRequired()
                .HasColumnName("created_at");

            builder.Property(o => o.TotalAmount)
                .IsRequired()
                .HasColumnName("total_amount")
                .HasColumnType("decimal(18,2)");

            builder.HasOne<Invoice>()
                .WithOne()
                .HasForeignKey<Invoice>(i => i.OrderId);
        }
    }
}
