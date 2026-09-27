using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategories;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests.Features.MarketCategoryInstruments;

public sealed class MarketCategoryInstrumentPolicyTests
{
    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3; Non-negotiable Behaviour 2.
    /// Verifies: due-slot selection uses the configured local trading day and partitions only the active schedule window.
    /// Expected: the instant maps to the second of four local slots even though UTC and local dates differ.
    /// Why: durable slots must be keyed by the applied schedule's local day, never by UTC midnight.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldUseLocalTradingDayAndSlot_WhenUtcDateDiffersFromScheduleDate()
    {
        var policy = CreatePolicy(new DateTimeOffset(2026, 3, 30, 1, 30, 0, TimeSpan.Zero));
        var schedule = CreateSchedule("America/New_York", new TimeOnly(21, 0), new TimeOnly(23, 0), [DayOfWeek.Sunday]);

        var decision = policy.Evaluate(CreateRequest(schedule, new MarketCategoryInstrumentFrequency(4, null, null)));

        Assert.True(decision.IsDue);
        Assert.Equal(new DateOnly(2026, 3, 29), decision.TradingDay);
        Assert.Equal(1, decision.SlotIndex);
        Assert.Equal(4, decision.EffectiveUpdatesPerDay);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3; Non-negotiable Behaviour 2.
    /// Verifies: due slots use start-inclusive and end-exclusive schedule boundaries.
    /// Expected: the opening instant is due in slot zero and the closing instant is blocked.
    /// Why: boundary semantics must match TradingScheduleGate and must not cause after-hours provider requests.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldIncludeOpeningAndExcludeClosing_WhenAtWindowBoundaries()
    {
        var schedule = CreateSchedule("UTC", new TimeOnly(9, 0), new TimeOnly(17, 0), [DayOfWeek.Monday]);
        var opening = CreatePolicy(new DateTimeOffset(2026, 3, 30, 9, 0, 0, TimeSpan.Zero))
            .Evaluate(CreateRequest(schedule, new MarketCategoryInstrumentFrequency(2, null, null)));
        var closing = CreatePolicy(new DateTimeOffset(2026, 3, 30, 17, 0, 0, TimeSpan.Zero))
            .Evaluate(CreateRequest(schedule, new MarketCategoryInstrumentFrequency(2, null, null)));

        Assert.True(opening.IsDue);
        Assert.Equal(0, opening.SlotIndex);
        Assert.False(closing.IsDue);
        Assert.Equal(MarketCategoryInstrumentScheduleBlockReason.ScheduleInactive, closing.BlockReason);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3; Non-negotiable Behaviour 2 and 3.
    /// Verifies: schedule evaluation refuses collection on weekends and configured local holidays.
    /// Expected: both otherwise in-window instants are blocked as inactive.
    /// Why: the configured day policy and holiday exclusions must suppress all provider requests.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldBlockWeekendAndHoliday_WhenScheduleIsInactiveForLocalDate()
    {
        var holiday = new DateOnly(2026, 3, 30);
        var schedule = CreateSchedule(
            "UTC",
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            [holiday]);
        var frequency = new MarketCategoryInstrumentFrequency(1, null, null);
        var weekend = CreatePolicy(new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.Zero))
            .Evaluate(CreateRequest(schedule, frequency));
        var excludedHoliday = CreatePolicy(new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero))
            .Evaluate(CreateRequest(schedule, frequency));

        Assert.Equal(MarketCategoryInstrumentScheduleBlockReason.ScheduleInactive, weekend.BlockReason);
        Assert.Equal(MarketCategoryInstrumentScheduleBlockReason.ScheduleInactive, excludedHoliday.BlockReason);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3; Non-negotiable Behaviour 2.
    /// Verifies: the local wall-clock active window is divided into approximately even slots.
    /// Expected: the exact half-window boundary belongs to slot one for a two-slot schedule.
    /// Why: predictable, non-overlapping slots prevent an arbitrary UTC cadence from drifting from operator hours.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldAdvanceAtEvenLocalSlotBoundary_WhenWindowIsDivided()
    {
        var policy = CreatePolicy(new DateTimeOffset(2026, 3, 30, 17, 0, 0, TimeSpan.Zero));
        var schedule = CreateSchedule("America/New_York", new TimeOnly(9, 0), new TimeOnly(17, 0), [DayOfWeek.Monday]);

        var decision = policy.Evaluate(CreateRequest(schedule, new MarketCategoryInstrumentFrequency(2, null, null)));

        Assert.True(decision.IsDue);
        Assert.Equal(1, decision.SlotIndex);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3; Non-negotiable Behaviour 2.
    /// Verifies: a spring DST jump marks local slots that never occurred as missed rather than shifting the trading day.
    /// Expected: after slot one, the wall-clock jump to slot three reports slot two as a gap.
    /// Why: skipped slots must be explicit and must never trigger out-of-window catch-up.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReportSkippedSlot_WhenSpringDstJumpsOverItsLocalInterval()
    {
        var schedule = CreateSchedule("America/New_York", new TimeOnly(0, 0), new TimeOnly(4, 0), [DayOfWeek.Sunday]);
        var progress = new MarketCategoryInstrumentSlotProgress(
            new DateOnly(2026, 3, 8),
            1,
            4,
            ScheduleIdentity(schedule));
        var policy = CreatePolicy(new DateTimeOffset(2026, 3, 8, 7, 30, 0, TimeSpan.Zero));

        var decision = policy.Evaluate(CreateRequest(
            schedule,
            new MarketCategoryInstrumentFrequency(4, null, null),
            progress));

        Assert.True(decision.IsDue);
        Assert.Equal(3, decision.SlotIndex);
        Assert.Equal([2], decision.MissedSlotIndexes);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3; Non-negotiable Behaviour 2.
    /// Verifies: the repeated fall DST wall-clock interval retains one local slot identity.
    /// Expected: a slot already observed during the first occurrence is not due again during the repeated hour.
    /// Why: clock rollback must not replay a completed environment/day/slot collection.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldNotReplaySlot_WhenFallDstRepeatsTheSameLocalInterval()
    {
        var schedule = CreateSchedule("America/New_York", new TimeOnly(0, 0), new TimeOnly(4, 0), [DayOfWeek.Sunday]);
        var progress = new MarketCategoryInstrumentSlotProgress(
            new DateOnly(2026, 11, 1),
            1,
            4,
            ScheduleIdentity(schedule));
        var policy = CreatePolicy(new DateTimeOffset(2026, 11, 1, 6, 30, 0, TimeSpan.Zero));

        var decision = policy.Evaluate(CreateRequest(
            schedule,
            new MarketCategoryInstrumentFrequency(4, null, null),
            progress));

        Assert.False(decision.IsDue);
        Assert.Equal(MarketCategoryInstrumentScheduleBlockReason.SlotAlreadyObserved, decision.BlockReason);
        Assert.Equal(1, decision.SlotIndex);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3.
    /// Verifies: schedule edits invalidate old progress while preserving explicit gaps in the new local schedule.
    /// Expected: the new schedule identity causes earlier slots to be reported missed rather than treated as completed.
    /// Why: old slot history cannot safely suppress collection under changed operator hours or exclusions.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldResetSlotProgress_WhenScheduleIdentityChanges()
    {
        var previousSchedule = CreateSchedule("UTC", new TimeOnly(8, 0), new TimeOnly(16, 0), [DayOfWeek.Monday]);
        var updatedSchedule = CreateSchedule("UTC", new TimeOnly(8, 0), new TimeOnly(18, 0), [DayOfWeek.Monday]);
        var progress = new MarketCategoryInstrumentSlotProgress(
            new DateOnly(2026, 3, 30),
            0,
            3,
            ScheduleIdentity(previousSchedule));
        var policy = CreatePolicy(new DateTimeOffset(2026, 3, 30, 15, 0, 0, TimeSpan.Zero));

        var decision = policy.Evaluate(CreateRequest(
            updatedSchedule,
            new MarketCategoryInstrumentFrequency(3, null, null),
            progress));

        Assert.True(decision.IsDue);
        Assert.Equal([0, 1], decision.MissedSlotIndexes);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3; Non-negotiable Behaviour 3 and 5.
    /// Verifies: re-evaluating the due policy after the active window closes blocks an in-flight cycle's next call.
    /// Expected: an initially due slot becomes inactive at the exclusive closing boundary.
    /// Why: each network action must recheck the schedule so an open-window cycle cannot continue after close.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldBlockNextCycleAction_WhenScheduleClosesMidCycle()
    {
        var schedule = CreateSchedule("UTC", new TimeOnly(9, 0), new TimeOnly(17, 0), [DayOfWeek.Monday]);
        var frequency = new MarketCategoryInstrumentFrequency(1, null, null);
        var beforeClose = CreatePolicy(new DateTimeOffset(2026, 3, 30, 16, 59, 59, TimeSpan.Zero))
            .Evaluate(CreateRequest(schedule, frequency));
        var afterClose = CreatePolicy(new DateTimeOffset(2026, 3, 30, 17, 0, 0, TimeSpan.Zero))
            .Evaluate(CreateRequest(schedule, frequency, new MarketCategoryInstrumentSlotProgress(
                new DateOnly(2026, 3, 30),
                0,
                1,
                beforeClose.ScheduleIdentity!)));

        Assert.True(beforeClose.IsDue);
        Assert.False(afterClose.IsDue);
        Assert.Equal(MarketCategoryInstrumentScheduleBlockReason.ScheduleInactive, afterClose.BlockReason);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3; Non-negotiable Behaviour 3 and 6.
    /// Verifies: a process starting partway through the active window reports earlier slots as gaps, not catch-up work.
    /// Expected: only the current slot is due and preceding slots are separately identified as missed.
    /// Why: restart recovery must not burst requests to replay slots that were not completed in their window.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldReportEarlierSlotsAsMissed_WhenStartingMidWindowWithoutProgress()
    {
        var schedule = CreateSchedule("UTC", new TimeOnly(8, 0), new TimeOnly(16, 0), [DayOfWeek.Monday]);
        var policy = CreatePolicy(new DateTimeOffset(2026, 3, 30, 13, 0, 0, TimeSpan.Zero));

        var decision = policy.Evaluate(CreateRequest(schedule, new MarketCategoryInstrumentFrequency(4, null, null)));

        Assert.True(decision.IsDue);
        Assert.Equal(2, decision.SlotIndex);
        Assert.Equal([0, 1], decision.MissedSlotIndexes);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3.
    /// Verifies: persisted legacy pending frequency data does not override the authoritative current count.
    /// Expected: the current count is returned both before and on the legacy effective date.
    /// Why: pending values must only be promoted by an explicit operator save, not by schedule policy.
    /// </summary>
    [Fact]
    public void ForTradingDay_ShouldKeepCurrentFrequency_WhenLegacyPendingValueExists()
    {
        var frequency = new MarketCategoryInstrumentFrequency(
            1,
            3,
            new DateOnly(2026, 3, 31));

        Assert.Equal(1, frequency.ForTradingDay(new DateOnly(2026, 3, 30)));
        Assert.Equal(1, frequency.ForTradingDay(new DateOnly(2026, 3, 31)));
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 1, step 2; Non-negotiable Behaviour 2.
    /// Verifies: zero disables timed starts while an arbitrarily high positive count remains valid.
    /// Expected: zero has no due slot or next timed start, while Int32.MaxValue is accepted at the opening.
    /// Why: operators need a real timed-off state and the old four-update cap must not reject configuration.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldAllowZeroAndUnboundedFrequency_WhenTimedScheduleIsEvaluated()
    {
        var schedule = CreateSchedule("UTC", new TimeOnly(8, 0), new TimeOnly(17, 0), [DayOfWeek.Monday]);
        var zeroPolicy = CreatePolicy(new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero));
        var zeroFrequency = new MarketCategoryInstrumentFrequency(0, null, null);
        var zeroDecision = zeroPolicy.Evaluate(CreateRequest(schedule, zeroFrequency));

        var unboundedDecision = CreatePolicy(new DateTimeOffset(2026, 3, 30, 8, 0, 0, TimeSpan.Zero))
            .Evaluate(CreateRequest(schedule, new MarketCategoryInstrumentFrequency(int.MaxValue, null, null)));

        Assert.False(zeroDecision.IsDue);
        Assert.Equal(MarketCategoryInstrumentScheduleBlockReason.TimedUpdatesDisabled, zeroDecision.BlockReason);
        Assert.Equal(0, zeroDecision.EffectiveUpdatesPerDay);
        Assert.Null(zeroPolicy.GetNextScheduledStartUtc(schedule, zeroFrequency.ForTradingDay(new DateOnly(2026, 3, 30))));
        Assert.True(unboundedDecision.IsDue);
        Assert.Equal(int.MaxValue, unboundedDecision.EffectiveUpdatesPerDay);
        Assert.Equal(0, unboundedDecision.SlotIndex);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 1, step 3.
    /// Verifies: slot starts partition the complete same-day window using equal local-time intervals.
    /// Expected: a 08:00–17:00 window with three starts reports 08:00, 11:00 and 14:00 as successive starts.
    /// Why: durable slot identity and operator previews must agree on deterministic wall-clock boundaries.
    /// </summary>
    [Fact]
    public void GetNextScheduledStartUtc_ShouldUseEqualWallClockIntervals_WhenWindowHasThreeStarts()
    {
        var schedule = CreateSchedule("UTC", new TimeOnly(8, 0), new TimeOnly(17, 0), [DayOfWeek.Monday]);
        var beforeOpening = CreatePolicy(new DateTimeOffset(2026, 3, 30, 7, 0, 0, TimeSpan.Zero))
            .GetNextScheduledStartUtc(schedule, 3);
        var betweenFirstAndSecond = CreatePolicy(new DateTimeOffset(2026, 3, 30, 9, 0, 0, TimeSpan.Zero))
            .GetNextScheduledStartUtc(schedule, 3);
        var betweenSecondAndThird = CreatePolicy(new DateTimeOffset(2026, 3, 30, 12, 0, 0, TimeSpan.Zero))
            .GetNextScheduledStartUtc(schedule, 3);

        Assert.Equal(new DateTimeOffset(2026, 3, 30, 8, 0, 0, TimeSpan.Zero), beforeOpening);
        Assert.Equal(new DateTimeOffset(2026, 3, 30, 11, 0, 0, TimeSpan.Zero), betweenFirstAndSecond);
        Assert.Equal(new DateTimeOffset(2026, 3, 30, 14, 0, 0, TimeSpan.Zero), betweenSecondAndThird);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 1, step 3.
    /// Verifies: a slot becomes current at its exact floor-rounded start, including a non-integral interval boundary.
    /// Expected: the first of seven slots is superseded exactly at 08:00 plus floor(nine hours / seven).
    /// Why: rounding must match the durable slot-start formula and must not defer admission by a tick.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldAdvanceAtFloorRoundedStart_WhenWindowDoesNotDivideEvenly()
    {
        var schedule = CreateSchedule("UTC", new TimeOnly(8, 0), new TimeOnly(17, 0), [DayOfWeek.Monday]);
        var slotStart = new TimeOnly(9, 17, 8).Add(TimeSpan.FromTicks(5_714_285));
        var now = new DateTimeOffset(new DateOnly(2026, 3, 30).ToDateTime(slotStart, DateTimeKind.Utc));
        var decision = CreatePolicy(now)
            .Evaluate(CreateRequest(schedule, new MarketCategoryInstrumentFrequency(7, null, null)));

        Assert.True(decision.IsDue);
        Assert.Equal(1, decision.SlotIndex);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1 configuration identity.
    /// Verifies schedule revisions include the applied profile and collection count/lead-in revisions.
    /// Expected: changing any identity or admission parameter changes the revision.
    /// Why: active runs must not continue under configuration values that an Operator has replaced.
    /// </summary>
    [Fact]
    public void GetScheduleRevision_ShouldChange_WhenAppliedProfileOrCollectionSettingsChange()
    {
        var schedule = CreateSchedule("UTC", new TimeOnly(8, 0), new TimeOnly(17, 0), [DayOfWeek.Monday]) with
        {
            AppliedBrokerEnvironmentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ScheduleVersion = 2
        };
        var frequency = new MarketCategoryInstrumentFrequency(3, null, null, 50, 15, 4);
        var revision = MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(schedule, frequency);

        Assert.NotEqual(revision, MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(
            schedule with { ScheduleVersion = 3 },
            frequency));
        Assert.NotEqual(revision, MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(
            schedule,
            frequency with { LeadInMinutes = 16 }));
        Assert.NotEqual(revision, MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(
            schedule,
            frequency with { ConfigurationVersion = 5 }));
        Assert.NotEqual(revision, MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(
            schedule,
            frequency with { CurrentUpdatesPerDay = 4 }));
    }

    /// <summary>
    /// Trace: Trading-Day Market Data delivery plan, Work Item 3, step 4.
    /// Verifies only future starts inside the configured lead-in interval are covered; disabled timed updates cover none.
    /// Expected: 10:45 covers the 11:00 slot with a 15-minute lead-in, while one tick earlier and count zero do not.
    /// Why: interest/manual runs must avoid queuing a duplicate update for a near-future slot without inventing a timed start.
    /// </summary>
    [Fact]
    public void GetSlotsCoveredByLeadIn_ShouldCoverOnlyFutureSlotsInsideLeadIn_WhenRunIsTriggered()
    {
        var tradingDay = new DateOnly(2026, 9, 28);
        var schedule = CreateSchedule("UTC", new TimeOnly(8, 0), new TimeOnly(17, 0), [DayOfWeek.Monday]);
        var frequency = new MarketCategoryInstrumentFrequency(3, null, null, 100, 15);
        var policy = CreatePolicy(new DateTimeOffset(2026, 9, 28, 10, 45, 0, TimeSpan.Zero));
        var covered = policy.GetSlotsCoveredByLeadIn(
            schedule,
            frequency,
            tradingDay,
            new DateTimeOffset(2026, 9, 28, 10, 45, 0, TimeSpan.Zero));

        Assert.Equal(
            [new MarketDataFullRunSlotIdentity(
                tradingDay,
                MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(schedule, frequency),
                1)],
            covered);
        Assert.Empty(policy.GetSlotsCoveredByLeadIn(
            schedule,
            frequency,
            tradingDay,
            new DateTimeOffset(2026, 9, 28, 10, 44, 59, TimeSpan.Zero)));
        Assert.Empty(policy.GetSlotsCoveredByLeadIn(
            schedule,
            frequency with { CurrentUpdatesPerDay = 0 },
            tradingDay,
            new DateTimeOffset(2026, 9, 28, 10, 45, 0, TimeSpan.Zero)));
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 1, step 3; Non-negotiable Behaviour 2.
    /// Verifies: a scheduled local start inside a spring-forward gap advances to the first valid instant.
    /// Expected: 02:30 in New York on the spring transition resolves to 03:00 local, or 07:00 UTC.
    /// Why: nonexistent wall-clock starts must not be interpreted using an invalid or host-dependent offset.
    /// </summary>
    [Fact]
    public void GetNextScheduledStartUtc_ShouldAdvanceAcrossDstGap_WhenOpeningTimeDoesNotExist()
    {
        var schedule = CreateSchedule("America/New_York", new TimeOnly(2, 30), new TimeOnly(4, 0), [DayOfWeek.Sunday]);
        var nextStart = CreatePolicy(new DateTimeOffset(2026, 3, 8, 5, 0, 0, TimeSpan.Zero))
            .GetNextScheduledStartUtc(schedule, 1);

        Assert.Equal(new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero), nextStart);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 1, step 3; Non-negotiable Behaviour 2.
    /// Verifies: an ambiguous fall-back start resolves to the first occurrence of its local wall time.
    /// Expected: 01:30 in New York on the autumn transition resolves to 05:30 UTC, not the repeated 06:30 UTC instant.
    /// Why: a repeated clock hour must not create a second scheduled start for the same local slot.
    /// </summary>
    [Fact]
    public void GetNextScheduledStartUtc_ShouldChooseFirstOccurrence_WhenOpeningTimeIsAmbiguous()
    {
        var schedule = CreateSchedule("America/New_York", new TimeOnly(1, 30), new TimeOnly(2, 30), [DayOfWeek.Sunday]);
        var nextStart = CreatePolicy(new DateTimeOffset(2026, 11, 1, 4, 0, 0, TimeSpan.Zero))
            .GetNextScheduledStartUtc(schedule, 1);

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), nextStart);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3.
    /// Verifies: effective frequency changes target the next configured local trading day, excluding weekends and holidays.
    /// Expected: a Friday save with a Monday holiday becomes effective Tuesday.
    /// Why: frequency changes must follow the active schedule rather than a UTC or calendar-day shortcut.
    /// </summary>
    [Fact]
    public void GetNextEffectiveTradingDay_ShouldSkipWeekendAndHoliday_WhenFindingLocalScheduleDay()
    {
        var policy = CreatePolicy(new DateTimeOffset(2026, 4, 3, 12, 0, 0, TimeSpan.Zero));
        var schedule = CreateSchedule(
            "UTC",
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            [new DateOnly(2026, 4, 6)]);

        var nextDay = policy.GetNextEffectiveTradingDay(schedule);

        Assert.Equal(new DateOnly(2026, 4, 7), nextDay);
    }

    /// <summary>
    /// Trace: Market categories next scheduled check after a trading-schedule edit.
    /// Verifies: the next wake-up uses the current schedule's Saturday inclusion rather than the previous weekday-only calendar.
    /// Expected: a Friday check moves from Monday to Saturday when Saturday is added, without changing the collection frequency.
    /// Why: the status API must expose the revised schedule to a page refreshing its collection status.
    /// </summary>
    [Fact]
    public void GetNextWakeUpUtc_ShouldMoveToSaturday_WhenSaturdayIsAddedToTradingSchedule()
    {
        var policy = CreatePolicy(new DateTimeOffset(2026, 9, 25, 17, 0, 0, TimeSpan.Zero));
        var weekdaySchedule = CreateSchedule(
            "UTC",
            new TimeOnly(8, 0),
            new TimeOnly(16, 0),
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);

        var before = policy.GetNextWakeUpUtc(weekdaySchedule, 1);
        var after = policy.GetNextWakeUpUtc(
            weekdaySchedule with { TradingDays = [.. weekdaySchedule.TradingDays, DayOfWeek.Saturday] }, 1);

        Assert.Equal(new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero), before);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 8, 0, 0, TimeSpan.Zero), after);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 3; Non-negotiable Behaviour 3.
    /// Verifies: disabled schedules, unsupported applied environments and negative frequencies fail closed.
    /// Expected: each unsafe configuration is blocked before a due slot is produced.
    /// Why: no invalid local or environment context may lead to provider activity.
    /// </summary>
    [Fact]
    public void Evaluate_ShouldBlockProviderEligibility_WhenScheduleEnvironmentOrFrequencyIsInvalid()
    {
        var schedule = CreateSchedule("UTC", new TimeOnly(9, 0), new TimeOnly(17, 0), [DayOfWeek.Monday]);
        var policy = CreatePolicy(new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.Zero));

        var disabled = policy.Evaluate(CreateRequest(schedule, new MarketCategoryInstrumentFrequency(1, null, null)) with { IsScheduleEnabled = false });
        var unsupported = policy.Evaluate(CreateRequest(schedule, new MarketCategoryInstrumentFrequency(1, null, null)) with { IsAppliedEnvironmentSupported = false });
        var reconciliationRequired = policy.Evaluate(CreateRequest(schedule, new MarketCategoryInstrumentFrequency(1, null, null)) with
        {
            IsLegacyScheduleReconciliationRequired = true
        });
        var invalidFrequency = policy.Evaluate(CreateRequest(schedule, new MarketCategoryInstrumentFrequency(-1, null, null)));
        var invalidTimeZone = policy.Evaluate(CreateRequest(
            schedule with { TimeZone = "Missing/Trading_Zone" },
            new MarketCategoryInstrumentFrequency(1, null, null)));

        Assert.Equal(MarketCategoryInstrumentScheduleBlockReason.ScheduleDisabled, disabled.BlockReason);
        Assert.Equal(MarketCategoryInstrumentScheduleBlockReason.UnsupportedAppliedEnvironment, unsupported.BlockReason);
        Assert.Equal(MarketCategoryInstrumentScheduleBlockReason.LegacyScheduleReconciliationRequired, reconciliationRequired.BlockReason);
        Assert.Equal(MarketCategoryInstrumentScheduleBlockReason.InvalidFrequency, invalidFrequency.BlockReason);
        Assert.Equal(MarketCategoryInstrumentScheduleBlockReason.InvalidSchedule, invalidTimeZone.BlockReason);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 4; Non-negotiable Behaviour 4.
    /// Verifies: a failed category refresh prevents any instrument collection plan from being produced.
    /// Expected: the plan records the prerequisite failure and contains no instrument categories.
    /// Why: instrument values must never be associated with a stale or unrefreshed category catalogue.
    /// </summary>
    [Fact]
    public void AfterCategoryRefresh_ShouldStopBeforeInstruments_WhenCategoryPrerequisiteFails()
    {
        var policy = new MarketCategoryInstrumentCyclePolicy();
        var result = policy.AfterCategoryRefresh(
            new MarketCategoriesRefreshOutcome.Failed(MarketCategoriesFailureCategory.Unavailable, "safe"),
            [new MarketCategoryInstrumentInterest("rates", true)]);

        Assert.Equal(MarketCategoryInstrumentCyclePlanStatus.CategoryPrerequisiteFailed, result.Status);
        Assert.Empty(result.CategoriesToCollect);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 4; Non-negotiable Behaviour 4.
    /// Verifies: an empty current interest intersection becomes a category-only cycle.
    /// Expected: the plan is idle and does not request instrument work.
    /// Why: selecting no categories must still permit catalogue refresh without unnecessary IG calls.
    /// </summary>
    [Fact]
    public void AfterCategoryRefresh_ShouldPlanCategoryOnlyCycle_WhenNoCurrentCategoryIsSelected()
    {
        var policy = new MarketCategoryInstrumentCyclePolicy();
        var result = policy.AfterCategoryRefresh(
            SavedCategories("rates", "indices"),
            [new MarketCategoryInstrumentInterest("rates", false)]);

        Assert.Equal(MarketCategoryInstrumentCyclePlanStatus.NoSelectedCurrentCategories, result.Status);
        Assert.Empty(result.CategoriesToCollect);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 4; Non-negotiable Behaviour 4 and 7.
    /// Verifies: only selected categories that still exist in the refreshed catalogue are scheduled.
    /// Expected: current selected categories are ordered for deterministic sequential work and removed selections remain dormant.
    /// Why: dormant selections must not be deleted or sent to IG when a category disappears.
    /// </summary>
    [Fact]
    public void AfterCategoryRefresh_ShouldIntersectCurrentCategoriesAndRetainDormantSelections_WhenPlanningCollection()
    {
        var policy = new MarketCategoryInstrumentCyclePolicy();
        var result = policy.AfterCategoryRefresh(
            SavedCategories("rates", "indices"),
            [
                new MarketCategoryInstrumentInterest("retired", true),
                new MarketCategoryInstrumentInterest("indices", true),
                new MarketCategoryInstrumentInterest("rates", true),
                new MarketCategoryInstrumentInterest("unselected", false)
            ]);

        Assert.Equal(MarketCategoryInstrumentCyclePlanStatus.CollectInstruments, result.Status);
        Assert.Equal(["indices", "rates"], result.CategoriesToCollect);
        Assert.Equal(["retired"], result.DormantSelectedCategories);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 1.
    /// Verifies: failed collection results expose only a typed safe failure and not an untrusted provider message.
    /// Expected: the failure retains its classification and retry decision without a raw diagnostic field.
    /// Why: provider payloads, credentials and quote diagnostics must not leak through inward contracts.
    /// </summary>
    [Fact]
    public void CollectionResult_ShouldCarryTypedSafeFailure_WhenProviderCollectionFails()
    {
        var failure = new MarketCategoryInstrumentFailure(
            MarketCategoryInstrumentFailureCategory.IncompleteCollection,
            true);
        var result = new MarketCategoryInstrumentCollectionResult.Failed(failure);

        Assert.Same(failure, Assert.IsType<MarketCategoryInstrumentCollectionResult.Failed>(result).Failure);
        Assert.Equal(MarketCategoryInstrumentFailureCategory.IncompleteCollection, failure.Category);
        Assert.True(failure.IsRetryable);
    }

    /// <summary>
    /// Trace: Market Category Instruments Research lines 181-194; Work Item 1, step 1.
    /// Verifies: the inward instrument contract retains every provider catalogue and observation field using exact numeric types.
    /// Expected: decimal market values, signed 64-bit epoch/popularity values, provider text, and nullable fields round-trip unchanged.
    /// Why: analysis must distinguish absent provider values from invented defaults and avoid floating-point loss.
    /// </summary>
    [Fact]
    public void MarketCategoryInstrument_ShouldPreserveProviderFieldsAndExactNumericTypes_WhenValuesArePresent()
    {
        var instrument = new MarketCategoryInstrument(
            "EPIC-1",
            "Example instrument",
            "INDICES",
            "Underlying",
            "-",
            0.25m,
            true,
            1000m,
            1_800_000_000_123L,
            "TRADEABLE",
            15,
            101.125m,
            101.25m,
            102.5m,
            99.75m,
            0.125m,
            0.1234m,
            "10:15:00",
            9_223_372_036_854_000_000L);

        Assert.Equal("EPIC-1", instrument.Epic);
        Assert.Equal("Example instrument", instrument.InstrumentName);
        Assert.Equal("INDICES", instrument.InstrumentType);
        Assert.Equal("Underlying", instrument.UnderlyingName);
        Assert.Equal("-", instrument.Expiry);
        Assert.Equal(0.25m, instrument.LotSize);
        Assert.True(instrument.OtcTradeable);
        Assert.Equal(1000m, instrument.ScalingFactor);
        Assert.Equal(1_800_000_000_123L, instrument.ExpiryTimestamp);
        Assert.Equal("TRADEABLE", instrument.MarketStatus);
        Assert.Equal(15, instrument.DelayTime);
        Assert.Equal(101.125m, instrument.Bid);
        Assert.Equal(101.25m, instrument.Offer);
        Assert.Equal(102.5m, instrument.High);
        Assert.Equal(99.75m, instrument.Low);
        Assert.Equal(0.125m, instrument.NetChange);
        Assert.Equal(0.1234m, instrument.PercentageChange);
        Assert.Equal(9_223_372_036_854_000_000L, instrument.Popularity);
        Assert.Equal("10:15:00", instrument.UpdateTime);
    }

    /// <summary>
    /// Trace: Market Category Instruments Research lines 182-194; Work Item 1, step 1.
    /// Verifies: provider-optional trading and volatile fields remain nullable rather than receiving synthetic values.
    /// Expected: a valid named instrument can retain null lot, tradeability, scaling, expiry, quote, status, and time values.
    /// Why: missing optional observations are not equivalent to zero, false, or a live quote.
    /// </summary>
    [Fact]
    public void MarketCategoryInstrument_ShouldAllowAbsentOptionalValues_WhenProviderOmitsThem()
    {
        var instrument = new MarketCategoryInstrument(
            "EPIC-2",
            "Unquoted instrument",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.Null(instrument.LotSize);
        Assert.Null(instrument.InstrumentType);
        Assert.Null(instrument.UnderlyingName);
        Assert.Null(instrument.Expiry);
        Assert.Null(instrument.OtcTradeable);
        Assert.Null(instrument.ScalingFactor);
        Assert.Null(instrument.ExpiryTimestamp);
        Assert.Null(instrument.MarketStatus);
        Assert.Null(instrument.DelayTime);
        Assert.Null(instrument.Bid);
        Assert.Null(instrument.Offer);
        Assert.Null(instrument.High);
        Assert.Null(instrument.Low);
        Assert.Null(instrument.NetChange);
        Assert.Null(instrument.PercentageChange);
        Assert.Null(instrument.UpdateTime);
        Assert.Null(instrument.Popularity);
    }

    /// <summary>
    /// Trace: Market Category Instruments Research lines 50-62; Work Item 1, step 1.
    /// Verifies: run provenance carries the applied endpoint profile, UTC time, complete paging metadata, and platform quality status.
    /// Expected: all evidence required to explain a successful collection remains attached to its run.
    /// Why: later analysis must be able to distinguish a validated complete observation from an unverified or partial result.
    /// </summary>
    [Fact]
    public void MarketCategoryInstrumentRunProvenance_ShouldRetainEndpointAndCompletenessEvidence_WhenConstructed()
    {
        var metadata = new MarketCategoryInstrumentCollectionMetadata(
            150,
            [1, 2, 3],
            3,
            312);
        var quality = new MarketCategoryInstrumentDataQualityEvidence(
            MarketCategoryInstrumentDataQualityStatus.CompleteValidatedWithOptionalValuesMissing,
            312,
            4);
        var retrievedAtUtc = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        var provenance = new MarketCategoryInstrumentRunProvenance(
            Guid.NewGuid(),
            BrokerEnvironmentKind.Demo,
            "ig-demo-v1",
            "indices",
            1,
            new DateOnly(2026, 9, 24),
            1,
            2,
            retrievedAtUtc,
            metadata,
            quality,
            Guid.NewGuid(),
            1);

        Assert.Equal("ig-demo-v1", provenance.AppliedEndpointProfile);
        Assert.Equal(retrievedAtUtc, provenance.RetrievedAtUtc);
        Assert.Equal(150, provenance.CollectionMetadata.PageSize);
        Assert.Equal([1, 2, 3], provenance.CollectionMetadata.PageNumbersFetched);
        Assert.Equal(3, provenance.CollectionMetadata.ProviderTotalPages);
        Assert.Equal(312, provenance.CollectionMetadata.ProviderTotalResults);
        Assert.Equal(MarketCategoryInstrumentDataQualityStatus.CompleteValidatedWithOptionalValuesMissing, provenance.DataQualityEvidence.Status);
        Assert.Equal(312, provenance.DataQualityEvidence.ValidatedInstrumentCount);
        Assert.Equal(4, provenance.DataQualityEvidence.MissingOptionalValueCount);
    }

    /// <summary>
    /// Trace: Market Category Instruments Work Item 1, step 4; Non-negotiable Behaviour 4 and 5.
    /// Verifies: retries are bounded and stop when schedule, lease, environment, allowance or safe failure policy blocks them.
    /// Expected: no more than two additional eligible attempts are allowed; quota and environmental blocks never retry.
    /// Why: retries must remain inside the active safe budget and cannot turn a provider failure into a request burst.
    /// </summary>
    [Fact]
    public void CanRetry_ShouldEnforceTwoRetryLimitAndSafetyGates_WhenAttemptingRecovery()
    {
        var policy = new MarketCategoryInstrumentRetryPolicy();
        var transientFailure = new MarketCategoryInstrumentFailure(
            MarketCategoryInstrumentFailureCategory.Unavailable,
            true);
        var rateLimitedFailure = new MarketCategoryInstrumentFailure(
            MarketCategoryInstrumentFailureCategory.RateLimited,
            true);

        Assert.True(policy.CanRetry(transientFailure, 0, true, true, true, true));
        Assert.True(policy.CanRetry(transientFailure, 1, true, true, true, true));
        Assert.False(policy.CanRetry(transientFailure, 2, true, true, true, true));
        Assert.False(policy.CanRetry(transientFailure, 0, false, true, true, true));
        Assert.False(policy.CanRetry(transientFailure, 0, true, false, true, true));
        Assert.False(policy.CanRetry(transientFailure, 0, true, true, false, true));
        Assert.False(policy.CanRetry(transientFailure, 0, true, true, true, false));
        Assert.False(policy.CanRetry(rateLimitedFailure, 0, true, true, true, true));
    }

    private static MarketCategoryInstrumentSchedulePolicy CreatePolicy(DateTimeOffset utcNow) =>
        new(new TradingScheduleGate(), new TimeProviderMarketCategoryInstrumentClock(new FixedTimeProvider(utcNow)));

    private static MarketCategoryInstrumentScheduleRequest CreateRequest(
        TradingScheduleConfiguration schedule,
        MarketCategoryInstrumentFrequency frequency,
        MarketCategoryInstrumentSlotProgress? progress = null) =>
        new(true, true, BrokerEnvironmentKind.Demo, schedule, frequency, progress);

    private static TradingScheduleConfiguration CreateSchedule(
        string timeZone,
        TimeOnly start,
        TimeOnly end,
        IReadOnlyList<DayOfWeek> tradingDays,
        IReadOnlyList<DateOnly>? holidays = null) =>
        new(start, end, tradingDays, WeekendBehavior.ExcludeWeekends, holidays ?? [], timeZone);

    private static string ScheduleIdentity(TradingScheduleConfiguration schedule)
    {
        var activeDays = string.Join(",", schedule.TradingDays.Distinct().Order());
        var holidays = string.Join(
            ",",
            schedule.BankHolidayExclusions
                .Distinct()
                .Order()
                .Select(date => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
        return string.Join(
            "|",
            schedule.TimeZone,
            schedule.StartOfDay.ToString("HH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture),
            schedule.EndOfDay.ToString("HH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture),
            schedule.WeekendBehavior,
            activeDays,
            holidays);
    }

    private static MarketCategoriesRefreshOutcome SavedCategories(params string[] categoryCodes) =>
        new MarketCategoriesRefreshOutcome.Saved(new MarketCategorySnapshot(
            categoryCodes.Select(code => new MarketCategory(code, false)).ToArray(),
            new DateTimeOffset(2026, 3, 30, 9, 0, 0, TimeSpan.Zero)));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
