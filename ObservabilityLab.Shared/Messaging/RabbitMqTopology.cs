namespace ObservabilityLab.Shared.Messaging;

/// <summary>
/// Single source of truth for the RabbitMQ topology defined in 05-contrato-eventos.yaml.
/// All exchange names, routing keys, and queue names are declared here as constants
/// to avoid hardcoded strings across publisher and consumer code.
/// </summary>
public static class RabbitMqTopology
{
    public static class Exchanges
    {
        public const string OrderEvents = "orders.events";
    }

    public static class RoutingKeys
    {
        public const string OrderCreated = "order.created";
        public const string OrderProcessed = "order.processed";
        public const string InvoiceGenerated = "invoice.generated";
        public const string EmailSent = "email.sent";
    }

    public static class Queues
    {
        public const string OrderProcessingWorker = "order-processing-worker";
        public const string InvoiceWorker = "invoice-worker";
        public const string EmailWorker = "email-worker";
    }

    /// <summary>
    /// Bindings to create: (queueName, routingKey).
    /// email.sent has no consumer in v1.0.0 and is intentionally omitted.
    /// </summary>
    public static readonly IReadOnlyList<(string Queue, string RoutingKey)> Bindings =
    [
        (Queues.OrderProcessingWorker, RoutingKeys.OrderCreated),
        (Queues.InvoiceWorker,         RoutingKeys.OrderProcessed),
        (Queues.EmailWorker,           RoutingKeys.InvoiceGenerated),
    ];
}
