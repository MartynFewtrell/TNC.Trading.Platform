namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal enum MarketCategoryProviderErrorCodeParseOutcome
{
    Parsed,
    Missing,
    Unknown,
    Malformed,
    NonJson,
    Oversized,
    Unsafe,
    Unavailable
}
