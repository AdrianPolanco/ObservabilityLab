

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObservabilityLab.Shared.Entities;

namespace ObservabilityLab.Shared.Database.Configurations
{
    internal class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.ToTable("customers");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name)
                .HasColumnName("name")
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.Email)
                .HasColumnName("email")
                .IsRequired();

            builder.HasIndex(c => c.Email);
        }
    }
}
