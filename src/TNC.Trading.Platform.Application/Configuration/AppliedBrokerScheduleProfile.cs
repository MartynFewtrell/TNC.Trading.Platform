namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record AppliedBrokerScheduleProfile(
    Guid BrokerEnvironmentId,
    int DefaultsVersion,
    TradingScheduleConfiguration TradingSchedule,
    bool LegacyReconciliationRequired = false,
    long ScheduleVersion = 1);
