namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>A nullable market-detail quantity preserving provider unit and value-presence state.</summary>
internal sealed record MarketDetailQuantityResponse(
    string Presence,
    decimal? Value,
    string? Unit);
