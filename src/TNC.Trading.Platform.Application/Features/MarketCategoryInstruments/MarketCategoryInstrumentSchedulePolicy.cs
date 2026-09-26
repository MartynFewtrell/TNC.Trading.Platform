using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Applies TradingScheduleGate semantics and splits only an active local window into slots.</summary>
internal sealed class MarketCategoryInstrumentSchedulePolicy(
    TradingScheduleGate tradingScheduleGate,
    IMarketCategoryInstrumentClock clock)
{
    private const int MaximumUpdatesPerDay = 4;

    public MarketCategoryInstrumentScheduleDecision Evaluate(MarketCategoryInstrumentScheduleRequest request)
    {
        if (!request.IsScheduleEnabled)
        {
            return Blocked(MarketCategoryInstrumentScheduleBlockReason.ScheduleDisabled);
        }

        if (!request.IsAppliedEnvironmentSupported || request.AppliedBrokerEnvironment is null)
        {
            return Blocked(MarketCategoryInstrumentScheduleBlockReason.UnsupportedAppliedEnvironment);
        }

        var schedule = request.Schedule;
        if (schedule.StartOfDay >= schedule.EndOfDay
            || !TradingScheduleGate.TryResolveTimeZone(schedule.TimeZone, out var timeZone))
        {
            return Blocked(MarketCategoryInstrumentScheduleBlockReason.InvalidSchedule);
        }

        if (!IsValidFrequency(request.Frequency))
        {
            return Blocked(MarketCategoryInstrumentScheduleBlockReason.InvalidFrequency);
        }

        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var tradingDay = tradingScheduleGate.GetTradingDay(schedule, nowUtc);
        var scheduleIdentity = GetScheduleIdentity(schedule);
        var effectiveFrequency = request.Frequency.ForTradingDay(tradingDay);

        var scheduleStatus = tradingScheduleGate.Evaluate(schedule, nowUtc);
        if (!scheduleStatus.IsActive)
        {
            if (tradingScheduleGate.IsTradingDay(schedule, tradingDay)
                && TimeOnly.FromDateTime(localNow.DateTime) >= schedule.EndOfDay)
            {
                var closedWindowProgressMatches = request.PreviousProgress is { } priorProgress
                    && priorProgress.TradingDay == tradingDay
                    && priorProgress.UpdatesPerDay == effectiveFrequency
                    && (string.Equals(priorProgress.ScheduleIdentity, scheduleIdentity, StringComparison.Ordinal)
                        || string.Equals(
                            priorProgress.ScheduleIdentity,
                            GetScheduleRevision(scheduleIdentity).ToString(CultureInfo.InvariantCulture),
                            StringComparison.Ordinal));
                var firstMissedSlot = closedWindowProgressMatches ? request.PreviousProgress!.SlotIndex + 1 : 0;
                var missingSlotIndexes = Enumerable.Range(
                    firstMissedSlot,
                    Math.Max(0, effectiveFrequency - firstMissedSlot)).ToArray();
                return new(
                    false,
                    MarketCategoryInstrumentScheduleBlockReason.ScheduleInactive,
                    tradingDay,
                    effectiveFrequency,
                    effectiveFrequency,
                    scheduleIdentity,
                    missingSlotIndexes);
            }

            return new(false, MarketCategoryInstrumentScheduleBlockReason.ScheduleInactive, tradingDay, null, null, scheduleIdentity, []);
        }

        var windowTicks = schedule.EndOfDay.Ticks - schedule.StartOfDay.Ticks;
        var elapsedTicks = TimeOnly.FromDateTime(localNow.DateTime).Ticks - schedule.StartOfDay.Ticks;
        var slotIndex = (int)Math.Min((long)effectiveFrequency - 1, elapsedTicks * effectiveFrequency / windowTicks);
        var previousProgressMatches = request.PreviousProgress is { } previous
            && previous.TradingDay == tradingDay
            && previous.UpdatesPerDay == effectiveFrequency
            && (string.Equals(previous.ScheduleIdentity, scheduleIdentity, StringComparison.Ordinal)
                || string.Equals(previous.ScheduleIdentity, GetScheduleRevision(scheduleIdentity).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));

        if (!request.IsStartupCheck && previousProgressMatches && request.PreviousProgress!.SlotIndex >= slotIndex)
        {
            return new(false, MarketCategoryInstrumentScheduleBlockReason.SlotAlreadyObserved, tradingDay, slotIndex, effectiveFrequency, scheduleIdentity, []);
        }

        var firstUnobservedSlot = previousProgressMatches ? request.PreviousProgress!.SlotIndex + 1 : 0;
        var missedSlots = Enumerable.Range(firstUnobservedSlot, Math.Max(0, slotIndex - firstUnobservedSlot)).ToArray();
        return new(true, null, tradingDay, slotIndex, effectiveFrequency, scheduleIdentity, missedSlots);
    }

    public DateOnly? GetNextEffectiveTradingDay(TradingScheduleConfiguration schedule)
    {
        if (schedule.StartOfDay >= schedule.EndOfDay
            || !TradingScheduleGate.TryResolveTimeZone(schedule.TimeZone, out _))
        {
            return null;
        }

        var today = tradingScheduleGate.GetTradingDay(schedule, clock.GetUtcNow());
        for (var daysAhead = 1; daysAhead <= 366; daysAhead++)
        {
            var candidate = today.AddDays(daysAhead);
            if (tradingScheduleGate.IsTradingDay(schedule, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public DateTimeOffset GetNextWakeUpUtc(TradingScheduleConfiguration schedule, int updatesPerDay)
    {
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        if (schedule.StartOfDay >= schedule.EndOfDay
            || updatesPerDay is < 1 or > MaximumUpdatesPerDay
            || !TradingScheduleGate.TryResolveTimeZone(schedule.TimeZone, out var timeZone))
        {
            return nowUtc.AddSeconds(30);
        }

        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        var localTime = TimeOnly.FromDateTime(localNow.DateTime);
        if (tradingScheduleGate.IsTradingDay(schedule, localDate))
        {
            if (localTime < schedule.StartOfDay)
            {
                return ResolveLocalInstant(localDate, schedule.StartOfDay, timeZone);
            }

            if (localTime < schedule.EndOfDay)
            {
                var elapsedTicks = localTime.Ticks - schedule.StartOfDay.Ticks;
                var windowTicks = schedule.EndOfDay.Ticks - schedule.StartOfDay.Ticks;
                var currentSlot = Math.Min(updatesPerDay - 1, (int)((long)elapsedTicks * updatesPerDay / windowTicks));
                var nextBoundarySlot = currentSlot + 1;
                if (nextBoundarySlot < updatesPerDay)
                {
                    var nextBoundaryTicks = schedule.StartOfDay.Ticks + windowTicks * nextBoundarySlot / updatesPerDay;
                    return ResolveLocalInstant(localDate, TimeOnly.FromTimeSpan(TimeSpan.FromTicks(nextBoundaryTicks)), timeZone);
                }
            }
        }

        for (var daysAhead = 1; daysAhead <= 366; daysAhead++)
        {
            var candidate = localDate.AddDays(daysAhead);
            if (tradingScheduleGate.IsTradingDay(schedule, candidate))
            {
                return ResolveLocalInstant(candidate, schedule.StartOfDay, timeZone);
            }
        }

        return nowUtc.AddSeconds(30);
    }

    public DateTimeOffset? GetWindowEndUtc(TradingScheduleConfiguration schedule, DateOnly tradingDay)
    {
        if (schedule.StartOfDay >= schedule.EndOfDay
            || !TradingScheduleGate.TryResolveTimeZone(schedule.TimeZone, out var timeZone))
        {
            return null;
        }

        var localEnd = tradingDay.ToDateTime(schedule.EndOfDay, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(localEnd))
        {
            localEnd = localEnd.AddMinutes(1);
        }

        if (timeZone.IsAmbiguousTime(localEnd))
        {
            return new DateTimeOffset(localEnd, timeZone.GetAmbiguousTimeOffsets(localEnd).Min()).ToUniversalTime();
        }

        return TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
    }

    private static DateTimeOffset ResolveLocalInstant(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var localDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(localDateTime))
        {
            localDateTime = localDateTime.AddMinutes(1);
        }

        if (timeZone.IsAmbiguousTime(localDateTime))
        {
            var earlierInstantOffset = timeZone.GetAmbiguousTimeOffsets(localDateTime).Max();
            return new DateTimeOffset(localDateTime, earlierInstantOffset).ToUniversalTime();
        }

        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
    }

    private static bool IsValidFrequency(MarketCategoryInstrumentFrequency frequency) =>
        frequency.CurrentUpdatesPerDay is >= 1 and <= MaximumUpdatesPerDay
        && (frequency.PendingUpdatesPerDay is null || frequency.PendingUpdatesPerDay is >= 1 and <= MaximumUpdatesPerDay)
        && ((frequency.PendingUpdatesPerDay is null) == (frequency.PendingEffectiveTradingDay is null));

    private static string GetScheduleIdentity(TradingScheduleConfiguration schedule)
    {
        var activeDays = string.Join(",", schedule.TradingDays.Distinct().Order());
        var holidays = string.Join(
            ",",
            schedule.BankHolidayExclusions
                .Distinct()
                .Order()
                .Select(date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        return string.Join(
            "|",
            schedule.TimeZone,
            schedule.StartOfDay.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            schedule.EndOfDay.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            schedule.WeekendBehavior,
            activeDays,
            holidays);
    }

    internal static long GetScheduleRevision(TradingScheduleConfiguration schedule) =>
        GetScheduleRevision(GetScheduleIdentity(schedule));

    private static long GetScheduleRevision(string scheduleIdentity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(scheduleIdentity));
        return BitConverter.ToInt64(hash, 0) & long.MaxValue;
    }

    private static MarketCategoryInstrumentScheduleDecision Blocked(MarketCategoryInstrumentScheduleBlockReason reason) =>
        new(false, reason, null, null, null, null, []);
}
