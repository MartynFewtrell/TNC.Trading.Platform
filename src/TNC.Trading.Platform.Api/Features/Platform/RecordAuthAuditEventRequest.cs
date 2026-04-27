namespace TNC.Trading.Platform.Api.Features.Platform;

internal sealed record RecordAuthAuditEventRequest(
    string EventType,
    string? Path,
    string? Scope);
