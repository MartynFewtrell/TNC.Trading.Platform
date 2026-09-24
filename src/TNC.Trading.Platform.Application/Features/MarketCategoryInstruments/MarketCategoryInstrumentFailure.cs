namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>A redacted failure classification without provider diagnostics or response content.</summary>
internal sealed record MarketCategoryInstrumentFailure(
    MarketCategoryInstrumentFailureCategory Category,
    bool IsRetryable);
