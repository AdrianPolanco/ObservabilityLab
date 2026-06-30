

namespace ObservabilityLab.Shared.Messaging.Contracts
{
    public record OrderCreated(Guid OrderId, Guid CustomerId, DateTime CreatedAt);
    public record OrderProcessed(Guid OrderId, decimal TotalAmount, DateTime CreatedAt);
    public record InvoiceGenerated(Guid InvoiceId, Guid OrderId, string FilePath, DateTime GeneratedAt);
}
