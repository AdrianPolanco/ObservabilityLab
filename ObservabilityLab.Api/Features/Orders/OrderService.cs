using Microsoft.EntityFrameworkCore;
using ObservabilityLab.Api.Features.Common;
using ObservabilityLab.Shared.Database;
using ObservabilityLab.Shared.Entities;
using ObservabilityLab.Shared.Results;

namespace ObservabilityLab.Api.Features.Orders
{
    public class OrderService(ApplicationDbContext dbContext)
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

            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync(cancellationToken); // one transaction: product UPDATEs + order INSERT + order_item INSERTs

            return Result<Order>.Success(order);
        }
    }
}
