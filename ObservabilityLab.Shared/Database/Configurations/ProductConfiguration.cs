
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
        }
    }
}
