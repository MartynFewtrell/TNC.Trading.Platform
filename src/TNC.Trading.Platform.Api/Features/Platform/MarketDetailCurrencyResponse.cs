namespace TNC.Trading.Platform.Api.Features.Platform;

/// <summary>Saved provider currency and exchange-rate information.</summary>
internal sealed record MarketDetailCurrencyResponse(
    string Code,
    string Symbol,
    decimal BaseExchangeRate,
    decimal ExchangeRate,
    bool IsDefault);
