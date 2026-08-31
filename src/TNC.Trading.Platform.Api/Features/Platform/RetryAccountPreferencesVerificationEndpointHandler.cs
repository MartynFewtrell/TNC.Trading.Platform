using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class RetryAccountPreferencesVerificationEndpointHandler
{
    public static async Task<IResult> HandleAsync(RetryAccountPreferencesHttpRequest request, ReconcileAccountPreferencesHandler handler, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AccountId))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.AccountId)] = ["An account ID is required."] });

        var result = await handler.HandleAsync(new ReconcileAccountPreferencesRequest(request.AccountId), cancellationToken);
        if (result.LeaseUnavailable) return TypedResults.Problem(statusCode: 409, title: "Account preferences verification is already in progress.");
        return result.State is null ? TypedResults.Problem(statusCode: 409, title: "Account preferences verification could not be started.") : TypedResults.Ok(result.State.ToResponse());
    }
}