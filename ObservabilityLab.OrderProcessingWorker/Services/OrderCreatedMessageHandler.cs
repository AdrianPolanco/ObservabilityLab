

using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Shared.Database;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Results;
using System.Diagnostics;
using static ObservabilityLab.Shared.Messaging.Constants.MessagingConstants;

namespace ObservabilityLab.OrderProcessingWorker.Services
{
    internal class OrderCreatedMessageHandler(
        ApplicationDbContext dbContext,
        RabbitMqPublisher publisher,
        ILogger<OrderCreatedMessageHandler> logger,
        ActivitySource activitySource) : IMessageHandler<OrderCreated>
    {
        public async Task<Result<OrderCreated>> HandleAsync(OrderCreated message, CancellationToken cancellationToken)
        {
            // RabbitMqConsumer<TMessage> already opened a "Consume order-processing-worker" Consumer span
            // (parented to the Api's Publish Message span via the extracted trace context) and it is
            // Activity.Current for the duration of this call. Starting this Internal span here nests the
            // business step underneath it — same pattern as OrderService.CreateAsync on the API side.
            using var processSpan = activitySource.StartActivity("Process OrderCreated", ActivityKind.Internal);
            processSpan?.SetTag("order.id", message.OrderId);
            processSpan?.SetTag("order.customer_id", message.CustomerId);

            logger.LogInformation("Processing OrderCreated event for order {OrderId}.", message.OrderId);

            var order = await dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == message.OrderId && o.CustomerId == message.CustomerId, cancellationToken);

            order.Place();
            processSpan?.AddEvent(new ActivityEvent("Order placed"));

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
                processSpan?.SetStatus(ActivityStatusCode.Error, "OrderProcessed publish failed");
                return Result<OrderCreated>.Failure(new Error("ProcessedOrderUnpublished", $"The order {order.Id} publishing failed", new() {
                    { "OrderId", order.Id }
                }));
            }

            logger.LogInformation("Order {OrderId} processed and OrderProcessed event published.", order.Id);
            processSpan?.SetTag("order.total_amount", order.TotalAmount);
            processSpan?.SetStatus(ActivityStatusCode.Ok);

            return Result<OrderCreated>.Success(message);
        }
    }
}
