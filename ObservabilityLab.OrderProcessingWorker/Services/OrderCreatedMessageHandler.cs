

using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Shared.Database;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Results;
using static ObservabilityLab.Shared.Messaging.Constants.MessagingConstants;

namespace ObservabilityLab.OrderProcessingWorker.Services
{
    internal class OrderCreatedMessageHandler(ApplicationDbContext dbContext, RabbitMqPublisher publisher) : IMessageHandler<OrderCreated>
    {
        public async Task<Result<OrderCreated>> HandleAsync(OrderCreated message, CancellationToken cancellationToken)
        {
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
                return Result<OrderCreated>.Failure(new Error("ProcessedOrderUnpublished", $"The order {order.Id} publishing failed"));
            }

            return Result<OrderCreated>.Success(message);
        }
    }
}
