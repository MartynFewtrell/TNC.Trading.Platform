using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class RecordAuthAuditEventEndpointHandler
{
    public static async Task<Results<Accepted, ValidationProblem>> HandleAsync(
        RecordAuthAuditEventRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        RecordAuthAuditEventHandler handler,
        CancellationToken cancellationToken)
    {
        if (!PlatformAuthAuditEventResolver.TryResolve(request, user, out var userName))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.EventType)] = ["The supplied authentication audit event type is not supported."]
            });
        }

        var correlationId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        await handler.HandleAsync(
            new TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent.RecordAuthAuditEventRequest(
                request.EventType,
                request.Path,
                request.Scope,
                userName,
                user.FindFirstValue(ClaimTypes.NameIdentifier),
                correlationId),
            cancellationToken);

        return TypedResults.Accepted("/api/platform/events?category=auth");
    }
}