// TEMPLATE — 4 copy-points when cloning for a new use-case:
//   1. Rename the class      : CreateOrder → <Verb><Entity>  (e.g. PlaceOrder, GetProduct)
//   2. Rename Request/Response records to match
//   3. Swap the service call : orderService.CreateAsync(...) → <service>.<Method>(...)
//   4. Adjust route + verb   : MapPost("/") → Map<Verb>("<route>")

using ObservabilityLab.Api.Features.Common;
using ObservabilityLab.Shared.Entities;

namespace ObservabilityLab.Api.Features.Orders.Create;

internal static class CreateOrder
{
    // ── Request / Response ────────────────────────────────────────────────────

    internal record OrderItemRequest(Guid ProductId, int Quantity);

    internal record CreateOrderRequest(Guid CustomerId, List<OrderItemRequest> Products);

    internal record CreateOrderResponse(Guid OrderId);

    // ── Endpoint registration ─────────────────────────────────────────────────

    internal static void MapEndpoint(IEndpointRouteBuilder group) =>
        group
            .MapPost("/", Handle)
            .WithName("CreateOrder")
            .WithSummary("Creates a new pending order for the given customer.")
            .Produces<CreateOrderResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    // ── Handler ───────────────────────────────────────────────────────────────

    private static async Task<IResult> Handle(
        CreateOrderRequest request,
        OrderService orderService,
        CancellationToken ct)
    {
        var lines = request.Products.Select(p => (p.ProductId, p.Quantity)).ToList();
        var result = await orderService.CreateAsync(request.CustomerId, lines, ct);
        return result.ToHttpResult(order =>
            TypedResults.Created($"/orders/{order.Id}", ToResponse(order)));
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static CreateOrderResponse ToResponse(Order order) =>
        new(order.Id);
}
