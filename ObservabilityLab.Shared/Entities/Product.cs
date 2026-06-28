

namespace ObservabilityLab.Shared.Entities
{
    public class Product : Entity
    {
        public Product() { }

        public required string Name { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsInStock { get; set; }  
    }
}
