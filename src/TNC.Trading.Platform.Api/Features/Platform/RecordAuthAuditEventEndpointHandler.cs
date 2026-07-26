using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class RecordAuthAuditEventEndpointHandler
{
    public static async Task<Results<Accepted, ValidationProblem>> HandleAsync(
        RecordAuthAuditEventRequest request,
        ClaimsPrincipal user,
        HttpContext httpContext,
        PlatformConfigurationService configurationService,
        IPlatformEventStore eventStore,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!PlatformAuthAuditEventResolver.TryResolve(request, user, out var record))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.EventType)] = ["The supplied authentication audit event type is not supported."]
            });
        }

        var configuration = await configurationService.GetCurrentAsync(cancellationToken);
        var correlationId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        await eventStore.AddAsync(
            new PlatformEventRecord(
                Category: "auth",
                EventType: request.EventType,
                PlatformEnvironment: configuration.PlatformEnvironment,
                BrokerEnvironment: configuration.BrokerEnvironment,
                Severity: record.Severity,
                Summary: record.Summary,
                Details: new
                {
                    record.UserName,
                    Subject = user.FindFirstValue(ClaimTypes.NameIdentifier),
                    request.Path,
                    request.Scope,
                    CorrelationId = correlationId
                },
                CorrelationId: correlationId,
                RetryCycleId: null,
                OccurredAtUtc: timeProvider.GetUtcNow()),
            cancellationToken);

        return TypedResults.Accepted("/api/platform/events?category=auth");
    }
}