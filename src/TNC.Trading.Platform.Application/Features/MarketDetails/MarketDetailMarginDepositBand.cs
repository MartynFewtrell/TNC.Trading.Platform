namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailMarginDepositBand(
    decimal Minimum,
    MarketDetailQuantity Maximum,
    decimal Margin,
    string Currency);
