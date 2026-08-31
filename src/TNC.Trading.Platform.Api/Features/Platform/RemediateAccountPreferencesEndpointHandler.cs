using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class RemediateAccountPreferencesEndpointHandler
{
    public static async Task<IResult> HandleAsync(RemediateAccountPreferencesHttpRequest request, RemediateAccountPreferencesHandler handler, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AccountId) || request.DesiredRevision is null || request.TrailingStopsEnabled is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request)] = ["Account ID, desired revision, and desired setting are required."] });

        var result = await handler.HandleAsync(new RemediateAccountPreferencesRequest(request.AccountId, request.DesiredRevision.Value, request.TrailingStopsEnabled.Value, "operator", Guid.NewGuid().ToString("N")), cancellationToken);
        if (result.StaleRevision) return TypedResults.Problem(statusCode: 409, title: "Account preferences revision is stale.");
        if (result.LeaseUnavailable) return TypedResults.Problem(statusCode: 409, title: "Account preferences remediation is already in progress.");
        return result.State is null ? TypedResults.Problem(statusCode: 503, title: "Account preferences remediation is unavailable.") : TypedResults.Ok(result.State.ToResponse());
    }
}