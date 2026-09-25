namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed class MarketCategoryInstrumentScheduleClosedException()
    : InvalidOperationException("The active trading schedule changed before publication.");
