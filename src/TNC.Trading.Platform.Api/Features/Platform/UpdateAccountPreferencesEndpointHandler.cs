using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Application.Features.AccountPreferences;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class UpdateAccountPreferencesEndpointHandler
{
    public static async Task<IResult> HandleAsync(UpdateAccountPreferencesHttpRequest request, ClaimsPrincipal user, SaveAccountPreferencesHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (request.TrailingStopsEnabled is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(request.TrailingStopsEnabled)] = ["A trailing-stops value is required."] });
        if (!httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey) || string.IsNullOrWhiteSpace(idempotencyKey))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["An Idempotency-Key header is required."] });

        var result = await handler.HandleAsync(
            new SaveAccountPreferencesCommand(request.TrailingStopsEnabled.Value, request.ExpectedRevision, idempotencyKey.ToString()),
            PlatformAuthAuditEventResolver.ResolveUserName(user), httpContext.TraceIdentifier, cancellationToken);
        return result.ToSaveHttpResult();
    }
}
