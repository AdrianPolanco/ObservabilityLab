namespace ObservabilityLab.Api.Features.Common;

internal static class ErrorCodes
{
    // ── Not-found codes (→ 404) ───────────────────────────────────────────────
    internal const string CustomerDoesNotExist = "CustomerDoesNotExist";
    internal const string ProductDoesNotExist  = "ProductDoesNotExist";
    internal const string OrderDoesNotExist = "OrderDoesNotExist";

    // ── Domain validation codes (→ 400) ──────────────────────────────────────
    internal const string InvalidCustomerId        = "InvalidCustomerId";
    internal const string NotEnoughStock           = "NotEnoughStock";
    internal const string InvalidRequestedQuantity = "InvalidRequestedQuantity";
    internal const string InvalidProduct           = "InvalidProduct";
    internal const string InvalidOrder             = "InvalidOrder";
    internal const string EmptyItems               = "EmptyItems";
    internal const string InvalidStatusTransition  = "InvalidStatusTransition";

    internal static readonly HashSet<string> NotFoundCodes =
    [
        CustomerDoesNotExist,
        ProductDoesNotExist,
        OrderDoesNotExist
    ];
}
