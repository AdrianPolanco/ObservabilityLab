

namespace ObservabilityLab.Shared.Entities
{
    public class Product : Entity
    {
        public Product() { }

        public required string Name { get; set; }
        public decimal Price { get; set; }
    }
}
