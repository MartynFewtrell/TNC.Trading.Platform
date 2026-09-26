namespace TNC.Trading.Platform.Application.Features.MarketDetails;

internal sealed record MarketDetailCurrency(
    string Code,
    string Symbol,
    decimal BaseExchangeRate,
    decimal ExchangeRate,
    bool IsDefault);
