namespace TNC.Trading.Platform.Web.Authentication;

internal sealed record PlatformAuthAuditRequest(
    string EventType,
    string? Path,
    string? Scope);
