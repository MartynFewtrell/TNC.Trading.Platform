namespace TNC.Trading.Platform.Api.Features.Platform;

internal sealed record PlatformAuthAuditRecord(
    string Summary,
    string Severity,
    string UserName);
