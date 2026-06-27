
using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Shared.Entities;

namespace ObservabilityLab.Shared.Database
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; private set; }
        public DbSet<Order> Orders { get; private set; }
        public DbSet<OrderItem> OrderItems { get; private set; }
        public DbSet<Customer> Customers { get; private set; }
        public DbSet<Invoice> Invoices { get; private set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}   
