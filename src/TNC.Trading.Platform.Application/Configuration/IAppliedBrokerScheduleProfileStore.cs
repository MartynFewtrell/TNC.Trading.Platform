namespace TNC.Trading.Platform.Application.Configuration;

internal interface IAppliedBrokerScheduleProfileStore
{
    Task<AppliedBrokerScheduleProfile> GetAppliedAsync(CancellationToken cancellationToken);

    Task<AppliedBrokerScheduleProfile> SaveAppliedAsync(
        TradingScheduleConfiguration tradingSchedule,
        string actor,
        CancellationToken cancellationToken);
}
