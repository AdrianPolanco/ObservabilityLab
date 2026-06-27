
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ObservabilityLab.Shared.Entities;

namespace ObservabilityLab.Shared.Database.Configurations
{
    internal class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.ToTable("invoices");

            builder.HasKey(i => i.Id);

            builder.Property(i => i.OrderId)
                .IsRequired()
                .HasColumnName("order_id");

            builder.HasIndex(i => i.OrderId)
                .IsUnique();

            builder.Property(i => i.FilePath)
                .IsRequired();

            builder.Property(i => i.CreatedAt)
                .IsRequired()
                .HasColumnName("created_at");

            builder.Property(i => i.EmailSent)
                .HasColumnName("email")
                .IsRequired();

            builder.Property(i => i.EmailSentAt)
                .HasColumnName("email_sent_at")
                .IsRequired(false);
        }
    }
}
