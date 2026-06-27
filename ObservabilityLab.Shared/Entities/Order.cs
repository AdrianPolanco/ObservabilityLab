
using ObservabilityLab.Shared.Results;

namespace ObservabilityLab.Shared.Entities
{
    public class Order: Entity
    {
        private Order() { }
        public Guid CustomerId { get; private set; }
        public OrderStatus Status { get; private set; } 
        public DateTime CreatedAt { get; private set; }
        public decimal TotalAmount { get; private set; }
        private List<OrderItem> _items = new List<OrderItem>();
        public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

        public static Result<Order> Create(Guid customerId)
        {
            var errors = new List<Error>();

            if(customerId == Guid.Empty)
            {
                errors.Add(new Error("InvalidCustomerId", "Customer ID must be a valid GUID."));
            }

            if (errors.Any())
            {
                return Result<Order>.Failure(errors);
            }

            var order = new Order
            {
                CustomerId = customerId,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                TotalAmount = 0
            };

            return Result<Order>.Success(order);
        }

        public Result<OrderItem> AddItem(Product product, int quantity)
        {
            var errors = new List<Error>();

            if(product == null)
            {
                errors.Add(new Error("InvalidProduct", "Product must be a valid instance."));
            }

            if(quantity <= 0)
            {
                errors.Add(new Error("InvalidQuantity", "Quantity must be a positive integer."));
            }

            if (errors.Any())
            {
                return Result<OrderItem>.Failure(errors);
            }

            var result = OrderItem.Create(this, product!, quantity);

            if (result.IsSuccess)
            {
                _items.Add(result.Data!);
            }

            return result;
        }

        public Result<Order> UpdateStatus(OrderStatus newStatus)
        {
            var errors = new List<Error>();

            if(Status == OrderStatus.Processed && newStatus == OrderStatus.Pending)
            {
                errors.Add(new Error("InvalidStatusTransition", "Cannot transition from Processed to Pending."));
            }

            if (errors.Any())
            {
                return Result<Order>.Failure(errors);
            }

            Status = newStatus;
            return Result<Order>.Success(this);
        }

        public Result<Order> Place()
        {
            var errors = new List<Error>();

            if (!Items.Any())
            {
                errors.Add(new Error("EmptyItems", "Order must contain at least one item."));
            }

            if (errors.Any())
            {
                return Result<Order>.Failure(errors);
            }

            Status = OrderStatus.Processing;

            CalculateTotalAmount();

            return Result<Order>.Success(this);
        }

        private decimal CalculateTotalAmount()
        {
            TotalAmount = _items.Sum(i => i.UnitPrice * i.Quantity);
            return TotalAmount;
        }
    }
}
