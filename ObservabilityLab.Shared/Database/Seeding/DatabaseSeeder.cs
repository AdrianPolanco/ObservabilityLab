using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Shared.Entities;

namespace ObservabilityLab.Shared.Database.Seeding;

public class DatabaseSeeder
{
    private readonly ApplicationDbContext _context;

    public DatabaseSeeder(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!await _context.Customers.AnyAsync(cancellationToken))
        {
            await SeedCustomersAsync(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (!await _context.Products.AnyAsync(cancellationToken))
        {
            await SeedProductsAsync(cancellationToken);
        }
    }

    private async Task SeedCustomersAsync(CancellationToken cancellationToken)
    {
        var customers = new List<Customer>
        {
            new() { Id = Guid.NewGuid(), Name = "John Doe", Email = "john.doe@example.com" },
            new() { Id = Guid.NewGuid(), Name = "Jane Smith", Email = "jane.smith@example.com" },
            new() { Id = Guid.NewGuid(), Name = "Bob Johnson", Email = "bob.johnson@example.com" },
            new() { Id = Guid.NewGuid(), Name = "Alice Williams", Email = "alice.williams@example.com" },
            new() { Id = Guid.NewGuid(), Name = "Charlie Brown", Email = "charlie.brown@example.com" },
            new() { Id = Guid.NewGuid(), Name = "Adrian Polanco", Email = "adrianhulu0611@gmail.com" }
        };

        await _context.Customers.AddRangeAsync(customers, cancellationToken);
    }

    private async Task SeedProductsAsync(CancellationToken cancellationToken)
    {
        const int productCount = 30000;
        const int batchSize = 1000;

        var productNames = new[]
        {
            "Wireless Headphones", "USB-C Cable", "Phone Case", "Screen Protector", "Portable Charger",
            "Laptop Stand", "Mechanical Keyboard", "Wireless Mouse", "Monitor Light", "Desk Lamp",
            "Coffee Maker", "Water Bottle", "Yoga Mat", "Resistance Bands", "Dumbbells",
            "Running Shoes", "Fitness Tracker", "Smart Watch", "Bluetooth Speaker", "Phone Mount",
            "Webcam", "Microphone", "USB Hub", "External SSD", "Memory Card",
            "Notebook", "Pen Set", "Desk Organizer", "Cable Manager", "Phone Stand",
            "Backpack", "Laptop Bag", "Travel Pillow", "Eye Mask", "Ear Plugs",
            "Kitchen Knife Set", "Cutting Board", "Cookware Set", "Food Storage", "Blender",
            "Toaster", "Microwave", "Air Fryer", "Vacuum Cleaner", "Mop",
            "Bed Sheets", "Pillow", "Mattress Cover", "Duvet Cover", "Throw Blanket",
            "Desk Chair", "Standing Desk", "Shelving Unit", "Storage Cabinet", "File Cabinet",
            "Desk Clock", "Wall Art", "Picture Frame", "Floor Mat", "Door Mat",
            "LED Strip Lights", "Smart Bulbs", "Table Fan", "Space Heater", "Air Purifier",
            "Humidifier", "Dehumidifier", "Thermometer", "Smoke Detector", "Fire Extinguisher"
        };

        for (int i = 0; i < productCount; i += batchSize)
        {
            var products = new List<Product>();
            int batchEnd = Math.Min(i + batchSize, productCount);

            for (int j = i; j < batchEnd; j++)
            {
                var baseNameIndex = j % productNames.Length;
                var baseName = productNames[baseNameIndex];
                var variant = j / productNames.Length;

                var name = variant > 0 ? $"{baseName} - Variant {variant + 1}" : baseName;
                var price = (decimal)(9.99 + (j % 890)) + ((j % 100) * 0.01m);

                bool isInStock = j % 5 != 0;
                int stockQuantity = isInStock ? 10 + (j % 100) : 0;

                products.Add(new Product
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Price = Math.Round(price, 2),
                    StockQuantity = stockQuantity,
                    IsInStock = isInStock
                });
            }

            await _context.Products.AddRangeAsync(products, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
