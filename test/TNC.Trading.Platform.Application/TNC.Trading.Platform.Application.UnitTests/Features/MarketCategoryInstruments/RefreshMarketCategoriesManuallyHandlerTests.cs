using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketCategoryInstruments;

public sealed class RefreshMarketCategoriesManuallyHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, step 3.
    /// Verifies: manual category refresh is rejected before provider access when the configured local schedule is inactive.
    /// Expected: the handler returns ScheduleClosed and the provider gateway is never called.
    /// Why: an Operator endpoint must not bypass the same in-window rule enforced for scheduled collection.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldRejectRefreshWithoutProviderCall_WhenScheduleIsInactive()
    {
        var schedule = CreateSchedule([DayOfWeek.Tuesday]);
        var gateway = new CategoryGateway();
        var handler = CreateHandler(schedule, gateway, new ScheduleGuard(isActive: true));

        var response = await handler.HandleAsync(new RefreshMarketCategoriesManuallyRequest(), CancellationToken.None);

        Assert.Equal(MarketCategoriesFailureCategory.ScheduleClosed, Assert.IsType<MarketCategoriesRefreshOutcome.Failed>(response.Outcome).Category);
        Assert.Equal(0, gateway.CallCount);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, step 3.
    /// Verifies: an otherwise active manual refresh revalidates the schedule immediately before the provider request.
    /// Expected: a failed schedule guard returns ScheduleClosed without contacting the provider.
    /// Why: schedule or applied-environment changes between initial eligibility and execution must fail closed.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldRevalidateBeforeProviderCall_WhenScheduleGuardClosesWindow()
    {
        var schedule = CreateSchedule([DayOfWeek.Monday]);
        var gateway = new CategoryGateway();
        var guard = new ScheduleGuard(isActive: false);
        var handler = CreateHandler(schedule, gateway, guard);

        var response = await handler.HandleAsync(new RefreshMarketCategoriesManuallyRequest(), CancellationToken.None);

        Assert.Equal(MarketCategoriesFailureCategory.ScheduleClosed, Assert.IsType<MarketCategoriesRefreshOutcome.Failed>(response.Outcome).Category);
        Assert.Equal(1, guard.CallCount);
        Assert.Equal(0, gateway.CallCount);
    }

    private static RefreshMarketCategoriesManuallyHandler CreateHandler(
        TradingScheduleConfiguration schedule,
        CategoryGateway gateway,
        ScheduleGuard guard)
    {
        var clock = new TestClock();
        var policy = new MarketCategoryInstrumentSchedulePolicy(new TradingScheduleGate(), clock);
        var refresh = new RefreshMarketCategoriesHandler(gateway, new CategoryStore(), TimeProvider.System, guard);
        return new(
            new AppliedEnvironmentResolver(),
            new PlatformConfigurationService(new ConfigurationStore(CreateSnapshot(schedule))),
            new FrequencyReader(),
            guard,
            policy,
            clock,
            refresh);
    }

    private static TradingScheduleConfiguration CreateSchedule(IReadOnlyList<DayOfWeek> tradingDays) =>
        new(new TimeOnly(8, 0), new TimeOnly(16, 0), tradingDays, WeekendBehavior.ExcludeWeekends, [], "UTC");

    private static PlatformConfigurationSnapshot CreateSnapshot(TradingScheduleConfiguration schedule) =>
        new(
            PlatformEnvironmentKind.Live,
            BrokerEnvironmentKind.Demo,
            schedule,
            new RetryPolicyConfiguration(1, 2, 2, 60, 5),
            new NotificationSettingsConfiguration("RecordedOnly", "operator@example.com"),
            new CredentialPresence(true, true, true),
            true,
            true,
            Now,
            false);

    private sealed class AppliedEnvironmentResolver : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(new(
                Guid.NewGuid(),
                "IG",
                "Demo",
                "Active",
                "Available",
                "IgDemo",
                true,
                true));

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(Guid brokerEnvironmentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ConfigurationStore(PlatformConfigurationSnapshot snapshot) : IPlatformConfigurationStore
    {
        public Task<PlatformConfigurationSnapshot> ApplyStartupConfigurationAsync(CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);

        public Task<PlatformConfigurationSnapshot> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);

        public Task<PlatformConfigurationSnapshot> GetRuntimeAsync(
            PlatformEnvironmentKind? platformEnvironment,
            BrokerEnvironmentKind? brokerEnvironment,
            CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class FrequencyReader : IMarketCategoryInstrumentFrequencyReader
    {
        public Task<MarketCategoryInstrumentFrequency> ReadAsync(
            BrokerEnvironmentKind appliedBrokerEnvironment,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MarketCategoryInstrumentFrequency(1, null, null, 10));
    }

    private sealed class ScheduleGuard(bool isActive) : IMarketCategoryInstrumentScheduleGuard
    {
        public int CallCount { get; private set; }

        public Task<bool> IsStillActiveAsync(
            BrokerEnvironmentKind environment,
            MarketCategoryInstrumentRequestBudgetContext context,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(isActive);
        }
    }

    private sealed class TestClock : IMarketCategoryInstrumentClock
    {
        public DateTimeOffset GetUtcNow() => Now;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
        public CancellationTokenSource CreateDeadlineCancellationSource(TimeSpan delay) => new(delay);
    }

    private sealed class CategoryGateway : IMarketCategoriesGateway
    {
        public int CallCount { get; private set; }

        public Task<MarketCategoriesGatewayResult> GetAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<MarketCategoriesGatewayResult>(new MarketCategoriesGatewayResult.Succeeded([]));
        }
    }

    private sealed class CategoryStore : IMarketCategorySnapshotStore
    {
        public Task<MarketCategorySnapshot?> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult<MarketCategorySnapshot?>(null);

        public Task<MarketCategorySnapshot> ReplaceAsync(MarketCategorySnapshot snapshot, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }
}
