

using ObservabilityLab.Shared.Results;

namespace ObservabilityLab.Shared.Entities
{
    public class Invoice: Entity
    {
        private Invoice() { }

        public Guid OrderId { get; private set; }
        public string FilePath { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public bool EmailSent { get; private set; }
        public DateTime? EmailSentAt { get; private set; }

        public static Result<Invoice> Create(Order order, string filePath)
        {
            if(order.Id == Guid.Empty)
            {
                return Result<Invoice>.Failure(new Error("InvalidOrderId", "Order ID cannot be empty.", new() {
                    { "OrderId", order.Id }
                }));
            }

            if(order.Status != OrderStatus.Processed)
            {
                return Result<Invoice>.Failure(new Error("OrderNotProcessed", $"The order {order.Id} has not been processed yet: An invoice cannot be issued.", new() {
                    { "OrderId", order.Id },
                    { "CurrentStatus", order.Status }
                }));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Result<Invoice>.Failure(new Error("InvalidFilePath", "File path cannot be null or empty.", new() {
                    { "FilePath", filePath }
                }));
            }

            var invoice = new Invoice
            {
                OrderId = order.Id,
                FilePath = filePath,
                CreatedAt = DateTime.UtcNow,
                EmailSent = false,
                EmailSentAt = null
            };
            return Result<Invoice>.Success(invoice);
        }

        public void MarkEmailAsSent()
        {
            EmailSent = true;
            EmailSentAt = DateTime.UtcNow;
        }
    }
}
