using ObservabilityLab.Api.Features.Common;
using ObservabilityLab.Shared.Entities;

namespace ObservabilityLab.Api.Features.Orders.Get
{
    internal static class GetOrder
    {
        internal record GetOrderResponse(Guid OrderId, OrderStatus Status, List<OrderItemDto> Items, Invoice? Invoice = null);
        internal record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);
        internal record OrderDto(Guid OrderId, OrderStatus Status, List<OrderItemDto> Items, Invoice? Invoice = null);
        internal static OrderItemDto ToDto(this OrderItem orderItem, Product product) => new(product.Id, product.Name, orderItem.UnitPrice, orderItem.Quantity);
        internal static OrderDto ToDto(this Order order, List<OrderItemDto> items, Invoice? invoice = null) => new(order.Id, order.Status, items, invoice);
        internal static GetOrderResponse ToResponse(this OrderDto orderDto) => new(orderDto.OrderId, orderDto.Status, orderDto.Items, orderDto.Invoice);
        internal static void MapEndpoint(IEndpointRouteBuilder group) =>
            group.MapGet("/{orderId}", Handle)
            .WithName("GetOrder")
            .WithSummary("Gets an order details, including its id, status, items and invoice (if it's already issued).")
            .Produces<GetOrderResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);


        private static async Task<IResult> Handle(Guid orderId, OrderService orderService, CancellationToken cancellationToken)
        {
            var result = await orderService.GetOrderAsync(orderId, cancellationToken);

            return result.ToHttpResult(orderDto => TypedResults.Ok(orderDto.ToResponse()));
        }
    }
}
