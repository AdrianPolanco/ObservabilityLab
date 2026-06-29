using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Api.Features.Common;
using ObservabilityLab.Shared.Database;
using ObservabilityLab.Shared.Entities;
using ObservabilityLab.Shared.Messaging;
using ObservabilityLab.Shared.Messaging.Constants;
using ObservabilityLab.Shared.Messaging.Contracts;
using ObservabilityLab.Shared.Results;
using static ObservabilityLab.Api.Features.Orders.Get.GetOrder;

namespace ObservabilityLab.Api.Features.Orders
{
    internal class OrderService(ApplicationDbContext dbContext, RabbitMqPublisher publisher)
    {
        public async Task<Result<Order>> CreateAsync(Guid customerId, List<(Guid productId, int quantity)> products, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var errors = new List<Error>();

            var doesCustomerExist = await dbContext.Customers.AnyAsync(c => c.Id.Equals(customerId), cancellationToken);

            if (!doesCustomerExist)
            {
                errors.Add(new(ErrorCodes.CustomerDoesNotExist, $"The customer with id {customerId} does not exist."));
                return Result<Order>.Failure(errors);
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
                        $"The product with id {productId} does not exist."));
            }

            if (errors.Count != 0)
                return Result<Order>.Failure(errors);

            var orderLines = products
                .Select(p => (Product: foundProducts[p.productId], p.quantity))
                .ToList();

            var orderResult = Order.Create(customerId);
            if (!orderResult.IsSuccess)
                return orderResult;

            var order = orderResult.Data!;

            foreach (var (product, quantity) in orderLines)
            {
                // AddItem reduces the tracked product's stock AND appends the OrderItem to order._items.
                // On failure (e.g. NotEnoughStock) the stock is NOT mutated and the item is NOT appended.
                var addItemResult = order.AddItem(product, quantity);
                if (!addItemResult.IsSuccess)
                    errors.AddRange(addItemResult.Errors);
            }

            if (errors.Count != 0)
                return Result<Order>.Failure(errors); // DbContext is request-scoped; in-memory mutations are discarded

            cancellationToken.ThrowIfCancellationRequested();
            dbContext.Orders.Add(order);
            cancellationToken.ThrowIfCancellationRequested();
            await dbContext.SaveChangesAsync(cancellationToken); // one transaction: product UPDATEs + order INSERT + order_item INSERTs
            await publisher.PublishAsync(
                MessagingConstants.Exchanges.OrderEvents,
                MessagingConstants.RoutingKeys.OrderCreated,
                new OrderCreated(order.Id, order.CustomerId, DateTime.UtcNow),
                cancellationToken);

            return Result<Order>.Success(order);
        }

        public async Task<Result<OrderDto>> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                    dbContext.Invoices.FirstOrDefault(inv => inv.OrderId == o.Id)))
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (dto is null)
                return Result<OrderDto>.Failure(
                    new Error(ErrorCodes.OrderDoesNotExist, $"The order with id {orderId} does not exist."));

            return Result<OrderDto>.Success(dto);
        }
    }
}
