using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

internal sealed record MarketCategoryInstrumentScheduleRequest(
    bool IsScheduleEnabled,
    bool IsAppliedEnvironmentSupported,
    BrokerEnvironmentKind? AppliedBrokerEnvironment,
    TradingScheduleConfiguration Schedule,
    MarketCategoryInstrumentFrequency Frequency,
    MarketCategoryInstrumentSlotProgress? PreviousProgress);
