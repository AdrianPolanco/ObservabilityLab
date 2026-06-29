using ObservabilityLab.Shared.Results;

namespace ObservabilityLab.Api.Features.Common;

internal static class ResultExtensions
{
    /// <summary>
    /// Maps a <see cref="Result{T}"/> to an HTTP result:
    /// success → <paramref name="onSuccess"/>; failure → 404 or 400 ProblemDetails
    /// depending on whether any error code is in <see cref="ErrorCodes.NotFoundCodes"/>.
    /// </summary>
    internal static IResult ToHttpResult<T>(
        this Result<T> result,
        Func<T, IResult> onSuccess) where T : class
    {
        if (result.IsSuccess)
            return onSuccess(result.Data!);

        var statusCode = result.Errors.Any(e => ErrorCodes.NotFoundCodes.Contains(e.Code))
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        return TypedResults.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["errors"] = result.Errors });
    }
}
