

using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Shared.Database;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Results;
using static ObservabilityLab.Shared.Messaging.Constants.MessagingConstants;

namespace ObservabilityLab.OrderProcessingWorker.Services
{
    internal class OrderCreatedMessageHandler(ApplicationDbContext dbContext, RabbitMqPublisher publisher, ILogger<OrderCreatedMessageHandler> logger) : IMessageHandler<OrderCreated>
    {
        public async Task<Result<OrderCreated>> HandleAsync(OrderCreated message, CancellationToken cancellationToken)
        {
            logger.LogInformation("Processing OrderCreated event for order {OrderId}.", message.OrderId);

            var order = await dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == message.OrderId && o.CustomerId == message.CustomerId, cancellationToken);

            order.Place();

            // OUTBOX?

            await dbContext.SaveChangesAsync(cancellationToken);

            var published = await publisher.PublishAsync(
                Exchanges.OrderEvents,
                RoutingKeys.OrderProcessed,
                new OrderProcessed(order.Id, order.TotalAmount, DateTime.UtcNow),
                cancellationToken);

            if (!published)
            {
                logger.LogWarning("Failed to publish OrderProcessed event for order {OrderId}.", order.Id);
                return Result<OrderCreated>.Failure(new Error("ProcessedOrderUnpublished", $"The order {order.Id} publishing failed", new() {
                    { "OrderId", order.Id }
                }));
            }

            logger.LogInformation("Order {OrderId} processed and OrderProcessed event published.", order.Id);

            return Result<OrderCreated>.Success(message);
        }
    }
}
