using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.ReconcilePlatformAuthentication;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests;

public sealed class UpdatePlatformInstrumentCollectionSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, step 2.
    /// Verifies: updating only the allowance preserves the existing current and pending frequency and its effective day.
    /// Expected: the persisted environment-scoped settings retain all omitted frequency values and store the new allowance.
    /// Why: partial operator configuration updates must not reset unrelated collection controls.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldPreserveFrequency_WhenOnlyAllowanceIsUpdated()
    {
        var effectiveDay = new DateOnly(2026, 9, 29);
        var settings = new FrequencySettingsStore(new(1, 2, effectiveDay, 30));
        var handler = CreateHandler(settings);

        await handler.HandleAsync(new(CreateUpdate(approvedAllowance: 40)), CancellationToken.None);

        Assert.Equal(new(1, 2, effectiveDay, 40), settings.Current);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 5, step 2.
    /// Verifies: a changed frequency becomes pending on the next local trading day and retains the current allowance.
    /// Expected: the current frequency is unchanged, pending frequency is 3 for Tuesday, and the allowance remains 30.
    /// Why: schedule-sensitive frequency changes must not alter an already-running local trading day.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldScheduleFrequencyForNextTradingDay_WhenFrequencyChanges()
    {
        var settings = new FrequencySettingsStore(new(1, null, null, 30));
        var handler = CreateHandler(settings);

        await handler.HandleAsync(new(CreateUpdate(instrumentUpdatesPerDay: 3)), CancellationToken.None);

        Assert.Equal(new(1, 3, new DateOnly(2026, 9, 29), 30), settings.Current);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 6, step 4.
    /// Verifies: a collector-status read failure after a successful configuration save is reported separately from settings availability.
    /// Expected: the saved frequency remains in the response with `StatusUnavailable` rather than a false settings failure.
    /// Why: status-query failures must not erase valid operator configuration or make a committed save appear unsuccessful.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldRetainFrequency_WhenCollectorStatusReadFails()
    {
        var settings = new FrequencySettingsStore(new(1, null, null, 30));
        var handler = CreateHandler(settings, new FailingStatusReader());

        var response = await handler.HandleAsync(
            new(CreateUpdate(approvedAllowance: 40)),
            CancellationToken.None);

        Assert.Equal("StatusUnavailable", response.InstrumentCollectionSettingsStatus);
        Assert.Equal(settings.Current, response.InstrumentCollectionFrequency);
    }

    private static UpdatePlatformConfigurationHandler CreateHandler(
        FrequencySettingsStore settings,
        IMarketCategoryInstrumentStatusReader? statusReader = null)
    {
        var committer = new Committer();
        var reconciler = new Reconciler();
        var resolver = new AppliedEnvironmentResolver();
        var policy = new MarketCategoryInstrumentSchedulePolicy(new TradingScheduleGate(), new TestClock());
        return new(
            committer,
            new ReconcilePlatformAuthenticationHandler(reconciler),
            new UpdatePlatformConfigurationValidator(),
            resolver,
            settings,
            settings,
            policy,
            statusReader,
            new TradingScheduleGate(),
            new FixedTimeProvider(Now));
    }

    private static PlatformConfigurationUpdate CreateUpdate(
        int? instrumentUpdatesPerDay = null,
        int? approvedAllowance = null) =>
        new(
            BrokerEnvironmentKind.Demo,
            new(new TimeOnly(8, 0), new TimeOnly(16, 0),
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                WeekendBehavior.ExcludeWeekends, [], "UTC"),
            new(1, 5, 2, 60, 5),
            new("RecordedOnly", "operator@example.com"),
            null,
            null,
            null,
            "unit-test",
            instrumentUpdatesPerDay,
            approvedAllowance);

    private static PlatformConfigurationSnapshot CreateSnapshot() =>
        new(
            PlatformEnvironmentKind.Live,
            BrokerEnvironmentKind.Demo,
            new(new TimeOnly(8, 0), new TimeOnly(16, 0), [DayOfWeek.Monday], WeekendBehavior.ExcludeWeekends, [], "UTC"),
            new(1, 5, 2, 60, 5),
            new("RecordedOnly", "operator@example.com"),
            new(true, true, true),
            true,
            true,
            Now,
            false);

    private sealed class Committer : IUpdatePlatformConfigurationCommitter
    {
        public Task<UpdatePlatformConfigurationResult> CommitAsync(
            PlatformConfigurationUpdate update,
            CancellationToken cancellationToken) =>
            Task.FromResult(new UpdatePlatformConfigurationResult(CreateSnapshot(), false));
    }

    private sealed class Reconciler : IPlatformAuthenticationReconciler
    {
        public Task<PlatformRuntimeState> ReconcileAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PlatformRuntimeState { SessionStatus = PlatformSessionStatus.Active });
    }

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

    private sealed class FrequencySettingsStore(MarketCategoryInstrumentFrequency initial) :
        IMarketCategoryInstrumentFrequencyReader,
        IMarketCategoryInstrumentFrequencyWriter
    {
        public MarketCategoryInstrumentFrequency Current { get; private set; } = initial;

        public Task<MarketCategoryInstrumentFrequency> ReadAsync(
            BrokerEnvironmentKind appliedBrokerEnvironment,
            CancellationToken cancellationToken) =>
            Task.FromResult(Current);

        public Task SaveAsync(
            BrokerEnvironmentKind appliedBrokerEnvironment,
            MarketCategoryInstrumentFrequency frequency,
            CancellationToken cancellationToken)
        {
            Current = frequency;
            return Task.CompletedTask;
        }
    }

    private sealed class TestClock : IMarketCategoryInstrumentClock
    {
        public DateTimeOffset GetUtcNow() => Now;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
        public CancellationTokenSource CreateDeadlineCancellationSource(TimeSpan delay) => new(delay);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FailingStatusReader : IMarketCategoryInstrumentStatusReader
    {
        public Task<MarketCategoryInstrumentCollectionStatus> ReadAsync(
            BrokerEnvironmentKind appliedBrokerEnvironment,
            DateOnly tradingDay,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("status store unavailable");
    }
}
