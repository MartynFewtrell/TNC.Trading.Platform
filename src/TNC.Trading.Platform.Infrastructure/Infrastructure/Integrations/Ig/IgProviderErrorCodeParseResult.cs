using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Infrastructure.Integrations.Ig;

internal sealed record IgProviderErrorCodeParseResult(
    string? SafeCode,
    MarketCategoryProviderErrorCodeParseOutcome Outcome);
