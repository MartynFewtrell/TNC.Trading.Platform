namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>A saved margin band with an explicit nullable upper bound.</summary>
internal sealed record MarketDetailMarginDepositBandResponse(
    decimal Min,
    MarketDetailQuantityResponse Max,
    decimal Margin,
    string Currency);
