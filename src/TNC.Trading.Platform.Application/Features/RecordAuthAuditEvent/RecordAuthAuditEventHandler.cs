using TNC.Trading.Platform.Application.Authentication;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent.Ports;

namespace TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent;

internal sealed class RecordAuthAuditEventHandler(
    IRecordAuthAuditEventConfigurationReader configurationReader,
    IRecordAuthAuditEventCommitter committer,
    TimeProvider timeProvider)
{
    public async Task<RecordAuthAuditEventResponse> HandleAsync(
        RecordAuthAuditEventRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (summary, severity) = CreateEventDescription(request);
        var configuration = await configurationReader.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var auditEvent = new PlatformEventRecord(
            Category: "auth",
            EventType: request.EventType,
            PlatformEnvironment: configuration.PlatformEnvironment,
            BrokerEnvironment: configuration.BrokerEnvironment,
            Severity: severity,
            Summary: summary,
            Details: new
            {
                request.UserName,
                request.Subject,
                request.Path,
                request.Scope,
                request.CorrelationId
            },
            CorrelationId: request.CorrelationId,
            RetryCycleId: null,
            OccurredAtUtc: timeProvider.GetUtcNow());

        await committer.CommitAsync(new RecordAuthAuditEventIntent(auditEvent), cancellationToken).ConfigureAwait(false);
        return new RecordAuthAuditEventResponse(IsRecorded: true);
    }

    private static (string Summary, string Severity) CreateEventDescription(RecordAuthAuditEventRequest request)
    {
        return request.EventType switch
        {
            PlatformAuthenticationDefaults.AuditEvents.SignInCompleted =>
                ($"Operator {request.UserName} completed sign-in.", "Information"),
            PlatformAuthenticationDefaults.AuditEvents.SignOutCompleted =>
                ($"Operator {request.UserName} completed sign-out.", "Information"),
            PlatformAuthenticationDefaults.AuditEvents.AccessDenied =>
                ($"Operator {request.UserName} was denied access to {request.Path ?? "a protected platform surface"}.", "Warning"),
            PlatformAuthenticationDefaults.AuditEvents.TokenAcquisitionFailed =>
                ($"Operator {request.UserName} could not acquire delegated access for {request.Scope ?? "the requested scope set"}.", "Warning"),
            _ => throw new ArgumentException("The supplied authentication audit event type is not supported.", nameof(request.EventType))
        };
    }
}