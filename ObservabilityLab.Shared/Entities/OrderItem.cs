

using ObservabilityLab.Shared.Results;

namespace ObservabilityLab.Shared.Entities
{
    public class OrderItem: Entity
    {
        private OrderItem() { }

        public Guid OrderId { get; private set; }
        public Guid ProductId { get; private set; }
        public int Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }

        public static Result<OrderItem> Create(Order order, Product product, int quantity)
        {
            var errors = new List<Error>();
            if (order == null)
            {
                errors.Add(new Error("InvalidOrder", "Order must be a valid instance."));
            }
            if(order!.Id == Guid.Empty)
            {
                errors.Add(new Error("InvalidOrderId", "Order ID must be a valid GUID."));
            }

            if (product == null)
            {
                errors.Add(new Error("InvalidProduct", "Product must be a valid instance."));
            }
            
            if (product!.Id == Guid.Empty)
            {
                errors.Add(new Error("InvalidProductId", "Product ID must be a valid GUID."));
            }

            if (quantity <= 0)
            {
                errors.Add(new Error("InvalidQuantity", "Quantity must be a positive integer."));
            }

            if (product.Price < 0)
            {
                errors.Add(new Error("InvalidUnitPrice", "Unit price cannot be negative."));
            }

            if (errors.Any())
            {
                return Result<OrderItem>.Failure(errors);
            }

            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = quantity,
                UnitPrice = product.Price
            };

            return Result<OrderItem>.Success(orderItem);
        }
    }
}
