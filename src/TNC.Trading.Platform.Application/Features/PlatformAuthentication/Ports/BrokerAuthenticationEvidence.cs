namespace TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

internal sealed record BrokerAuthenticationEvidence(
    string AccountId,
    string? StreamingEndpoint,
    DateTimeOffset? ExpiresAtUtc);