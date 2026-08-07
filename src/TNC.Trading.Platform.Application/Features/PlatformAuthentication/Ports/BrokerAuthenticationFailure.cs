namespace TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

internal sealed record BrokerAuthenticationFailure(
    BrokerAuthenticationFailureKind Kind,
    string Summary,
    BrokerAuthenticationDiagnostic? Diagnostic = null);

internal sealed record BrokerAuthenticationDiagnostic(
    string? ErrorCode,
    string? RequestId);