

namespace ObservabilityLab.Shared.Messaging.Contracts
{
    public record OrderCreated(Guid OrderId, Guid CustomerId, DateTime CreatedAt);
}
