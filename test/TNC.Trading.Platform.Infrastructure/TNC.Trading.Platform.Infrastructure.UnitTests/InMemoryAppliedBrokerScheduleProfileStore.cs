using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AppliedBrokerSchedule;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

internal sealed class InMemoryAppliedBrokerScheduleProfileStore(TradingScheduleConfiguration schedule)
    : IAppliedBrokerScheduleProfileStore
{
    public static InMemoryAppliedBrokerScheduleProfileStore ForConfiguration(
        Microsoft.Extensions.Configuration.IConfiguration configuration) =>
        new(string.IsNullOrWhiteSpace(configuration["Bootstrap:BrokerEnvironment"])
            ? AppliedBrokerScheduleDefaults.Create()
            : PlatformConfigurationBootstrapParser.Parse(configuration).TradingSchedule);

    private AppliedBrokerScheduleProfile profile = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        1,
        schedule);

    public Task<AppliedBrokerScheduleProfile> GetAppliedAsync(CancellationToken cancellationToken) =>
        Task.FromResult(profile);

    public Task<AppliedBrokerScheduleProfile> SaveAppliedAsync(
        TradingScheduleConfiguration tradingSchedule,
        string actor,
        CancellationToken cancellationToken)
    {
        profile = profile with
        {
            TradingSchedule = tradingSchedule,
            ScheduleVersion = checked(profile.ScheduleVersion + 1)
        };
        return Task.FromResult(profile);
    }
}
