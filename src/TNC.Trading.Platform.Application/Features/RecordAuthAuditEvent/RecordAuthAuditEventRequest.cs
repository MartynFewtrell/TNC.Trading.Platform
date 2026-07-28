namespace TNC.Trading.Platform.Application.Features.RecordAuthAuditEvent;

internal sealed record RecordAuthAuditEventRequest(
    string EventType,
    string? Path,
    string? Scope,
    string UserName,
    string? Subject,
    string CorrelationId);