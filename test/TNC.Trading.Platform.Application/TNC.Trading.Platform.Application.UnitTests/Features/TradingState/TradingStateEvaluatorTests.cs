using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.TradingState;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests.Features.TradingState;

public sealed class TradingStateEvaluatorTests
{
    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1, divergent readiness.
    /// Verifies market-data admission does not depend on order-trading readiness.
    /// Expected: a valid market-data-capable applied broker can start an update while the trade session is degraded.
    /// Why: market-data permission and order-placement readiness are independent capabilities.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldAllowMarketDataButBlockTrading_WhenTradeSessionIsNotReady()
    {
        var request = CreateRequest(
            new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero),
            sessionStatus: PlatformSessionStatus.Degraded);

        var state = CreateEvaluator(request.NowUtc).Evaluate(request);

        Assert.True(state.TradingWindowOpen);
        Assert.False(state.CanTrade);
        Assert.True(state.CanStartMarketDataUpdate);
        Assert.Contains(TradingStateBlockReason.TradingReadinessUnavailable, state.TradeBlockReasons);
        Assert.Empty(state.MarketDataBlockReasons);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1, divergent readiness.
    /// Verifies trading readiness does not grant market-data access or bypass the approved request allowance.
    /// Expected: an active trade session can trade while a missing allowance blocks market-data admission.
    /// Why: IG request quota is a separate safety constraint from broker-session readiness.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldAllowTradingButBlockMarketData_WhenAllowanceIsNotApproved()
    {
        var request = CreateRequest(
            new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero),
            allowance: null);

        var state = CreateEvaluator(request.NowUtc).Evaluate(request);

        Assert.True(state.CanTrade);
        Assert.False(state.CanStartMarketDataUpdate);
        Assert.Empty(state.TradeBlockReasons);
        Assert.Contains(TradingStateBlockReason.RequestAllowanceNotApproved, state.MarketDataBlockReasons);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1, zero timed updates.
    /// Verifies a zero timed-update count removes scheduled starts without disabling manual market-data admission.
    /// Expected: the window and market-data capability remain available while the next timed start is null.
    /// Why: operators must still be able to request a full update when periodic collection is disabled.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldKeepManualMarketDataCapability_WhenTimedUpdateCountIsZero()
    {
        var request = CreateRequest(
            new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero),
            updatesPerDay: 0);

        var state = CreateEvaluator(request.NowUtc).Evaluate(request);

        Assert.True(state.TradingWindowOpen);
        Assert.True(state.CanStartMarketDataUpdate);
        Assert.Null(state.NextScheduledStartUtc);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1, applied broker identity.
    /// Verifies a configured broker environment cannot use a different applied broker's trading permissions.
    /// Expected: both trading and market-data admission are blocked with an applied-broker mismatch reason.
    /// Why: selected configuration must not authorize activity against a different runtime broker.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldBlockBothCapabilities_WhenAppliedBrokerDoesNotMatchConfiguration()
    {
        var request = CreateRequest(
            new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero),
            appliedEnvironment: "Live");

        var state = CreateEvaluator(request.NowUtc).Evaluate(request);

        Assert.False(state.TradingWindowOpen);
        Assert.False(state.CanTrade);
        Assert.False(state.CanStartMarketDataUpdate);
        Assert.Contains(TradingStateBlockReason.AppliedBrokerMismatch, state.TradeBlockReasons);
        Assert.Contains(TradingStateBlockReason.AppliedBrokerMismatch, state.MarketDataBlockReasons);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1, applied broker identity.
    /// Verifies matching broker kinds do not conceal a schedule profile bound to a different catalog record.
    /// Expected: both capabilities are blocked with the applied-broker mismatch reason.
    /// Why: schedule identity must be tied to the actual applied environment, not only its Demo/Live kind.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldBlockBothCapabilities_WhenScheduleProfileBelongsToAnotherAppliedRecord()
    {
        var request = CreateRequest(
            new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero),
            scheduleEnvironmentIdMatches: false);

        var state = CreateEvaluator(request.NowUtc).Evaluate(request);

        Assert.False(state.TradingWindowOpen);
        Assert.False(state.CanTrade);
        Assert.False(state.CanStartMarketDataUpdate);
        Assert.Contains(TradingStateBlockReason.AppliedBrokerMismatch, state.TradeBlockReasons);
        Assert.Contains(TradingStateBlockReason.AppliedBrokerMismatch, state.MarketDataBlockReasons);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1, next-window reporting.
    /// Verifies an active window reports its next opening and its current closing boundary.
    /// Expected: the next opening is the following eligible day and the closing is today's end-exclusive instant.
    /// Why: operators need UTC boundaries consistent with the same calendar used for admission.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReportNextOpeningAndCurrentClosing_WhenInsideTradingWindow()
    {
        var request = CreateRequest(new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero));

        var state = CreateEvaluator(request.NowUtc).Evaluate(request);

        Assert.Equal(new DateTimeOffset(2026, 3, 31, 8, 0, 0, TimeSpan.Zero), state.NextWindowOpeningUtc);
        Assert.Equal(new DateTimeOffset(2026, 3, 30, 17, 0, 0, TimeSpan.Zero), state.NextWindowClosingUtc);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1, broker-calendar boundaries.
    /// Verifies future window boundaries skip a configured holiday after the current window has closed.
    /// Expected: opening and closing are reported for the next eligible local trading day, in UTC.
    /// Why: status guidance must use the same holiday calendar as runtime admission.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldSkipHoliday_WhenReportingNextWindowBoundaries()
    {
        var request = CreateRequest(new DateTimeOffset(2026, 3, 30, 18, 0, 0, TimeSpan.Zero));
        var schedule = request.Configuration.TradingSchedule with
        {
            BankHolidayExclusions = [new DateOnly(2026, 3, 31)]
        };
        request = request with
        {
            Configuration = request.Configuration with { TradingSchedule = schedule }
        };

        var state = CreateEvaluator(request.NowUtc).Evaluate(request);

        Assert.Equal(new DateTimeOffset(2026, 4, 1, 8, 0, 0, TimeSpan.Zero), state.NextWindowOpeningUtc);
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 17, 0, 0, TimeSpan.Zero), state.NextWindowClosingUtc);
    }

    private static TradingStateEvaluator CreateEvaluator(DateTimeOffset nowUtc)
    {
        var clock = new TimeProviderMarketCategoryInstrumentClock(new FixedTimeProvider(nowUtc));
        return new(new TradingScheduleGate(), new MarketCategoryInstrumentSchedulePolicy(new TradingScheduleGate(), clock));
    }

    private static TradingStateEvaluationRequest CreateRequest(
        DateTimeOffset nowUtc,
        PlatformSessionStatus sessionStatus = PlatformSessionStatus.Active,
        bool canAccessMarketData = true,
        int? allowance = 100,
        int updatesPerDay = 3,
        string appliedEnvironment = "Demo",
        bool scheduleEnvironmentIdMatches = true)
    {
        var appliedBrokerEnvironmentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var schedule = new TradingScheduleConfiguration(
            new TimeOnly(8, 0),
            new TimeOnly(17, 0),
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            WeekendBehavior.ExcludeWeekends,
            [],
            "UTC",
            scheduleEnvironmentIdMatches
                ? appliedBrokerEnvironmentId
                : Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            4);
        var configuration = new PlatformConfigurationSnapshot(
            PlatformEnvironmentKind.Development,
            BrokerEnvironmentKind.Demo,
            schedule,
            new(1, 5, 2, 60, 5),
            new("RecordedOnly", null),
            new(false, false, false),
            false,
            false,
            nowUtc,
            false);
        var applied = new AppliedBrokerEnvironmentContext(
            appliedBrokerEnvironmentId,
            "IG",
            appliedEnvironment,
            "Active",
            "Available",
            "IgDemo",
            true,
            canAccessMarketData);
        var runtimeState = new PlatformRuntimeState
        {
            SessionStatus = sessionStatus
        };

        return new(
            applied,
            configuration,
            new MarketCategoryInstrumentFrequency(updatesPerDay, null, null, allowance),
            runtimeState,
            nowUtc);
    }

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }
}
