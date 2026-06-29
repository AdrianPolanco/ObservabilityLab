using ObservabilityLab.Api.Features.Orders.Create;

namespace ObservabilityLab.Api.Features.Orders;

internal static class OrderEndpoints
{
    internal static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        CreateOrder.MapEndpoint(group);
        // GetOrderById.MapEndpoint(group);
        // ListOrders.MapEndpoint(group);
        // AddOrderItem.MapEndpoint(group);

        return app;
    }
}
