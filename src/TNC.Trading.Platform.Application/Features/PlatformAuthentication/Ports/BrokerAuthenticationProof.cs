namespace TNC.Trading.Platform.Application.Features.PlatformAuthentication.Ports;

internal sealed record BrokerAuthenticationProof(
    string? PreferredAccountName,
    string? PreferredAccountId,
    decimal? Balance,
    int OpenPositionCount);