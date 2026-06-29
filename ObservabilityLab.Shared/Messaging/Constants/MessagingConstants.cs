

namespace ObservabilityLab.Shared.Messaging.Constants
{
    public static class MessagingConstants
    {
        public static class RoutingKeys
        {
            public const string OrderCreated = "order.created";
            public const string OrderProcessed = "order.processed";
        }

        public static class Exchanges
        {
            public const string OrderEvents = "orders.events";
        }
    }
}
