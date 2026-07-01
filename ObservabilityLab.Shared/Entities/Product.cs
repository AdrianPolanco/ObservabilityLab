

using ObservabilityLab.Shared.Results;

namespace ObservabilityLab.Shared.Entities
{
    public class Product : Entity
    {
        public Product() { }

        public required string Name { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsInStock { get; set; }  

        public static Result<Product> Create(string name, decimal price, int stockQuantity = 0)
        {
            var errors = new List<Error>();

            if(stockQuantity < 0) { 
                errors.Add(new("StockQuantityNegative", "Stock quantity cannot be negative.", new() {
                    { "Name", name},
                    {"StockQuantity", stockQuantity},
                    {"Price", price}
                }));
            }

            if(price < 0) { 
                errors.Add(new("PriceNegative", "Price cannot be negative.", new(){{"Price", price}}));
            }

            if(string.IsNullOrWhiteSpace(name)) {
                errors.Add(new("EmptyProductName", "Product name cannot be empty.", new(){{"Name", name}}));
            }

            if(errors.Any()) {
                return Result<Product>.Failures(errors);
            }

            var isInStock = stockQuantity == 0 ? false : true;

            return Result<Product>.Success(new Product { Name = name, Price = price, StockQuantity = stockQuantity, IsInStock = isInStock });
        }

        public Result<Product> ReduceStock(int requestedQuantity)
        {
            var errors = new List<Error>();

            if(requestedQuantity <= 0)
            {
                errors.Add(new("InvalidRequestedQuantity", $"You cannot add {requestedQuantity} existences to the order, add 1 existence or more.", new() {
                    { "RequestedQuantity", requestedQuantity }
                }));
            }

            var isAvailable = CheckStock(requestedQuantity);

            if(!isAvailable) {
                errors.Add(new("NotEnoughStock", $"There aren't enough existences: You requested {requestedQuantity} existence, but there are only {StockQuantity} existences of this item.", new() {
                    { "RequestedQuantity", requestedQuantity },
                    { "StockQuantity", StockQuantity }
                }));
            }

            if(errors.Any()) {
                return Result<Product>.Failures(errors);
            }

            StockQuantity -= requestedQuantity;

            if(StockQuantity <= 0) IsInStock = false;

            return Result<Product>.Success(this);
        }

        private bool CheckStock(int requiredQuantity)
        {
            return StockQuantity >= requiredQuantity;
        }
    }
}
