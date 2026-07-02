using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Api.Features.Common;
using ObservabilityLab.Shared.Database;
using ObservabilityLab.Shared.Entities;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Constants;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Results;
using Serilog.Context;
using System.Diagnostics;
using static ObservabilityLab.Api.Features.Orders.Get.GetOrder;

namespace ObservabilityLab.Api.Features.Orders
{
    internal class OrderService(
        ApplicationDbContext dbContext, 
        RabbitMqPublisher publisher, 
        ILogger<OrderService> logger,
        ActivitySource activitySource)
    {
        public async Task<Result<Order>> CreateAsync(Guid customerId, List<(Guid productId, int quantity)> products, CancellationToken cancellationToken)
        {
            // ActivityKind.Internal: in-process business logic, as opposed to Server (inbound HTTP,
            // already opened automatically by ASP.NET Core's instrumentation as the parent of this span),
            // Client (an outbound call, e.g. the EF Core spans this method's queries generate), or
            // Producer/Consumer (the messaging hop below). StartActivity can return null when nothing is
            // sampling, so every member access on it uses `?.` — instrumentation must never be able to
            // throw and break the actual request.
            using var createOrderSpan = activitySource.StartActivity("Create Order", ActivityKind.Internal);
            createOrderSpan?.SetTag("order.customer_id", customerId);
            createOrderSpan?.SetTag("order.requested_item_count", products.Count);

            using (LogContext.PushProperty("CustomerId", customerId))
            {
                logger.LogInformation("Creating order for customer {CustomerId} with items: {@Products}", customerId, products);
                cancellationToken.ThrowIfCancellationRequested();
                var errors = new List<Error>();

                var doesCustomerExist = await dbContext.Customers.AnyAsync(c => c.Id.Equals(customerId), cancellationToken);

                if (!doesCustomerExist)
                {
                    errors.Add(new(ErrorCodes.CustomerDoesNotExist, $"The customer with id {customerId} does not exist.", new() {
                            { "CustomerId", customerId }
                        }));
                    var result = Result<Order>.Failures(errors);
                    logger.LogInformation("Customer validation failed: {@Result}", result);
                    createOrderSpan?.AddEvent(new ActivityEvent("Customer validation failed"));
                    createOrderSpan?.SetStatus(ActivityStatusCode.Error, "Customer validation failed");
                    return result;
                }

                var productIds = products.Select(p => p.productId).Distinct().ToList();

                cancellationToken.ThrowIfCancellationRequested();

                var foundProducts = await dbContext.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

                foreach (var productId in productIds)
                {
                    if (!foundProducts.ContainsKey(productId))
                        errors.Add(new(ErrorCodes.ProductDoesNotExist,
                            $"The product with id {productId} does not exist.", new() {
                                    { "ProductId", productId }
                            }));
                }

                if (errors.Count != 0)
                {
                    var result = Result<Order>.Failures(errors);
                    logger.LogInformation("Validation for products failed: {@ProductsResult}", result);
                    createOrderSpan?.AddEvent(new ActivityEvent("Product validation failed"));
                    createOrderSpan?.SetStatus(ActivityStatusCode.Error, "Product validation failed");
                    return result;
                }

                var orderLines = products
                    .Select(p => (Product: foundProducts[p.productId], p.quantity))
                    .ToList();

                var orderResult = Order.Create(customerId);
                if (!orderResult.IsSuccess)
                {
                    logger.LogInformation("Validation for order creation failed: {@OrderResult}", orderResult);
                    createOrderSpan?.AddEvent(new ActivityEvent("Order creation validation failed"));
                    createOrderSpan?.SetStatus(ActivityStatusCode.Error, "Order creation validation failed");
                    return orderResult;
                }

                var order = orderResult.Data!;
                createOrderSpan?.SetTag("order.id", order.Id);

                foreach (var (product, quantity) in orderLines)
                {
                    // AddItem reduces the tracked product's stock AND appends the OrderItem to order._items.
                    // On failure (e.g. NotEnoughStock) the stock is NOT mutated and the item is NOT appended.
                    var addItemResult = order.AddItem(product, quantity);
                    if (!addItemResult.IsSuccess)
                        errors.AddRange(addItemResult.Errors);
                }

                if (errors.Count != 0)
                {
                    var result = Result<Order>.Failures(errors); // DbContext is request-scoped; in-memory mutations are discarded
                    logger.LogInformation("Adding items to the order {OrderId} failed: {@OrderItemsResult}", order.Id, result);
                    createOrderSpan?.AddEvent(new ActivityEvent("Adding items to the order failed"));
                    createOrderSpan?.SetStatus(ActivityStatusCode.Error, "Adding items to the order failed");
                    return result;
                }
                cancellationToken.ThrowIfCancellationRequested();
                dbContext.Orders.Add(order);
                cancellationToken.ThrowIfCancellationRequested();
                await dbContext.SaveChangesAsync(cancellationToken); // one transaction: product UPDATEs + order INSERT + order_item INSERTs
                // publisher.PublishAsync opens its own Producer span nested under this one — the trace
                // tree so far is: Server (ASP.NET Core) -> Create Order (this span) -> EF Core client
                // spans for the queries above -> Publish Message (Producer). The consumer on the other
                // side of the queue picks this trace back up via MessagingTraceContext.
                var published = await publisher.PublishAsync(
                    MessagingConstants.Exchanges.OrderEvents,
                    MessagingConstants.RoutingKeys.OrderCreated,
                    new OrderCreated(order.Id, order.CustomerId, DateTime.UtcNow),
                    cancellationToken);

                logger.LogInformation("Order {OrderId} created successfully. Publish status: {@Published}", order.Id, published);
                createOrderSpan?.SetTag("order.total_amount", order.TotalAmount);
                createOrderSpan?.SetTag("order.item_count", order.Items.Count);
                createOrderSpan?.SetStatus(ActivityStatusCode.Ok, "Order created successfully");
                return Result<Order>.Success(order);
            }
        }

        public async Task<Result<OrderDto>> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var getOrderSpan = activitySource.StartActivity("Get Order", ActivityKind.Internal);
            getOrderSpan?.SetTag("order.id", orderId);

            // Single round trip: order + items joined to product names + invoice (left-join via correlated subquery).
            // AsNoTracking because this is a pure read — no change tracking overhead needed.
            var dto = await dbContext.Orders
                .Where(o => o.Id == orderId)
                .Select(o => new OrderDto(
                    o.Id,
                    o.Status,
                    o.Items
                        .Join(dbContext.Products,
                              i => i.ProductId,
                              p => p.Id,
                              (i, p) => new OrderItemDto(p.Id, p.Name, i.UnitPrice, i.Quantity))
                        .ToList(),
                    o.TotalAmount,
                    dbContext.Invoices.FirstOrDefault(inv => inv.OrderId == o.Id)))
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (dto is null){
                var result = Result<OrderDto>.Failure(
                    new Error(ErrorCodes.OrderDoesNotExist, $"The order with id {orderId} does not exist.", new() {
                        { "OrderId", orderId }
                    }));
                logger.LogInformation("Order {OrderId} not found: {@Result}", orderId, result);
                getOrderSpan?.SetStatus(ActivityStatusCode.Error, "Order not found");
                return result;
            }

            getOrderSpan?.SetStatus(ActivityStatusCode.Ok, "Order found");
            return Result<OrderDto>.Success(dto);
        }
    }
}
