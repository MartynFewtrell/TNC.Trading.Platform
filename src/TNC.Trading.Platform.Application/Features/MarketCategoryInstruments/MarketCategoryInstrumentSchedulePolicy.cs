using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

/// <summary>Applies TradingScheduleGate semantics and splits only an active local window into slots.</summary>
internal sealed class MarketCategoryInstrumentSchedulePolicy(
    TradingScheduleGate tradingScheduleGate,
    IMarketCategoryInstrumentClock clock)
{
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

        if (request.IsLegacyScheduleReconciliationRequired)
        {
            return Blocked(MarketCategoryInstrumentScheduleBlockReason.LegacyScheduleReconciliationRequired);
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
        var scheduleIdentity = GetScheduleIdentity(schedule, request.Frequency);
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
                    && MatchesScheduleIdentity(priorProgress.ScheduleIdentity, scheduleIdentity, schedule);
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

        if (effectiveFrequency == 0)
        {
            return new(
                false,
                MarketCategoryInstrumentScheduleBlockReason.TimedUpdatesDisabled,
                tradingDay,
                null,
                0,
                scheduleIdentity,
                []);
        }

        var windowTicks = schedule.EndOfDay.Ticks - schedule.StartOfDay.Ticks;
        var elapsedTicks = TimeOnly.FromDateTime(localNow.DateTime).Ticks - schedule.StartOfDay.Ticks;
        var slotIndex = GetCurrentSlotIndex(elapsedTicks, windowTicks, effectiveFrequency);
        var previousProgressMatches = request.PreviousProgress is { } previous
            && previous.TradingDay == tradingDay
            && previous.UpdatesPerDay == effectiveFrequency
            && MatchesScheduleIdentity(previous.ScheduleIdentity, scheduleIdentity, schedule);

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

    public DateTimeOffset? GetNextScheduledStartUtc(TradingScheduleConfiguration schedule, int updatesPerDay)
    {
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        if (schedule.StartOfDay >= schedule.EndOfDay
            || updatesPerDay <= 0
            || !TradingScheduleGate.TryResolveTimeZone(schedule.TimeZone, out var timeZone))
        {
            return null;
        }

        var localNow = TimeZoneInfo.ConvertTime(nowUtc, timeZone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        var localTime = TimeOnly.FromDateTime(localNow.DateTime);
        if (tradingScheduleGate.IsTradingDay(schedule, localDate))
        {
            var windowTicks = schedule.EndOfDay.Ticks - schedule.StartOfDay.Ticks;
            var firstCandidateSlot = 0;
            if (localTime >= schedule.StartOfDay && localTime < schedule.EndOfDay)
            {
                var elapsedTicks = localTime.Ticks - schedule.StartOfDay.Ticks;
                firstCandidateSlot = GetCurrentSlotIndex(elapsedTicks, windowTicks, updatesPerDay) + 1;
            }

            var nextSlotIndex = FindFirstSlotStartingAfter(
                localDate,
                schedule,
                timeZone,
                firstCandidateSlot,
                updatesPerDay,
                nowUtc);
            if (nextSlotIndex >= 0)
            {
                return GetSlotStartUtc(localDate, schedule, timeZone, nextSlotIndex, updatesPerDay);
            }
        }

        for (var daysAhead = 1; daysAhead <= 366; daysAhead++)
        {
            var candidate = localDate.AddDays(daysAhead);
            if (tradingScheduleGate.IsTradingDay(schedule, candidate))
            {
                var nextStart = GetSlotStartUtc(candidate, schedule, timeZone, 0, updatesPerDay);
                if (nextStart > nowUtc)
                {
                    return nextStart;
                }
            }
        }

        return null;
    }

    public IReadOnlyList<MarketDataFullRunSlotIdentity> GetSlotsCoveredByLeadIn(
        TradingScheduleConfiguration schedule,
        MarketCategoryInstrumentFrequency frequency,
        DateOnly tradingDay,
        DateTimeOffset admittedAtUtc)
    {
        if (schedule.StartOfDay >= schedule.EndOfDay
            || frequency.CurrentUpdatesPerDay <= 0
            || frequency.LeadInMinutes < 0
            || !tradingScheduleGate.IsTradingDay(schedule, tradingDay)
            || !TradingScheduleGate.TryResolveTimeZone(schedule.TimeZone, out var timeZone))
        {
            return [];
        }

        var admittedAt = admittedAtUtc.ToUniversalTime();
        var leadIn = TimeSpan.FromMinutes(frequency.LeadInMinutes);
        var covered = new List<MarketDataFullRunSlotIdentity>();
        for (var slotIndex = 0; slotIndex < frequency.CurrentUpdatesPerDay; slotIndex++)
        {
            var slotStartUtc = GetSlotStartUtc(
                tradingDay,
                schedule,
                timeZone,
                slotIndex,
                frequency.CurrentUpdatesPerDay);
            if (slotStartUtc >= admittedAt && slotStartUtc - admittedAt <= leadIn)
            {
                covered.Add(new(tradingDay, GetScheduleRevision(schedule, frequency), slotIndex));
            }
        }

        return covered;
    }

    public DateTimeOffset? GetNextWindowOpeningUtc(TradingScheduleConfiguration schedule)
    {
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        if (schedule.StartOfDay >= schedule.EndOfDay
            || !TradingScheduleGate.TryResolveTimeZone(schedule.TimeZone, out var timeZone))
        {
            return null;
        }

        var today = tradingScheduleGate.GetTradingDay(schedule, nowUtc);
        for (var daysAhead = 0; daysAhead <= 366; daysAhead++)
        {
            var candidate = today.AddDays(daysAhead);
            if (!tradingScheduleGate.IsTradingDay(schedule, candidate))
            {
                continue;
            }

            var opening = ResolveLocalInstant(candidate, schedule.StartOfDay, timeZone);
            if (opening > nowUtc)
            {
                return opening;
            }
        }

        return null;
    }

    public DateTimeOffset? GetNextWindowClosingUtc(TradingScheduleConfiguration schedule)
    {
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        if (schedule.StartOfDay >= schedule.EndOfDay
            || !TradingScheduleGate.TryResolveTimeZone(schedule.TimeZone, out _))
        {
            return null;
        }

        var today = tradingScheduleGate.GetTradingDay(schedule, nowUtc);
        for (var daysAhead = 0; daysAhead <= 366; daysAhead++)
        {
            var candidate = today.AddDays(daysAhead);
            if (!tradingScheduleGate.IsTradingDay(schedule, candidate))
            {
                continue;
            }

            var closing = GetWindowEndUtc(schedule, candidate);
            if (closing > nowUtc)
            {
                return closing;
            }
        }

        return null;
    }

    public DateTimeOffset GetNextWakeUpUtc(TradingScheduleConfiguration schedule, int updatesPerDay)
    {
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        return GetNextScheduledStartUtc(schedule, updatesPerDay) ?? nowUtc.AddSeconds(30);
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
            localDateTime = GetNextValidLocalTime(localDateTime, timeZone);
        }

        if (timeZone.IsAmbiguousTime(localDateTime))
        {
            var earlierInstantOffset = timeZone.GetAmbiguousTimeOffsets(localDateTime).Max();
            return new DateTimeOffset(localDateTime, earlierInstantOffset).ToUniversalTime();
        }

        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
    }

    private static int GetCurrentSlotIndex(long elapsedTicks, long windowTicks, int updatesPerDay) =>
        FindLastSlotStartingAtOrBefore(elapsedTicks, windowTicks, updatesPerDay);

    private static int FindLastSlotStartingAtOrBefore(long elapsedTicks, long windowTicks, int updatesPerDay)
    {
        var low = 0;
        var high = updatesPerDay - 1;
        var currentSlotIndex = 0;
        while (low <= high)
        {
            var candidateSlot = low + ((high - low) / 2);
            if (GetSlotStartTicks(windowTicks, candidateSlot, updatesPerDay) <= elapsedTicks)
            {
                currentSlotIndex = candidateSlot;
                low = candidateSlot + 1;
            }
            else
            {
                high = candidateSlot - 1;
            }
        }

        return currentSlotIndex;
    }

    private static int FindFirstSlotStartingAfter(
        DateOnly tradingDay,
        TradingScheduleConfiguration schedule,
        TimeZoneInfo timeZone,
        int firstCandidateSlot,
        int updatesPerDay,
        DateTimeOffset nowUtc)
    {
        var low = firstCandidateSlot;
        var high = updatesPerDay - 1;
        var nextSlotIndex = -1;
        while (low <= high)
        {
            var candidateSlot = low + ((high - low) / 2);
            var candidateStart = GetSlotStartUtc(tradingDay, schedule, timeZone, candidateSlot, updatesPerDay);
            if (candidateStart > nowUtc)
            {
                nextSlotIndex = candidateSlot;
                high = candidateSlot - 1;
            }
            else
            {
                low = candidateSlot + 1;
            }
        }

        return nextSlotIndex;
    }

    private static DateTimeOffset GetSlotStartUtc(
        DateOnly tradingDay,
        TradingScheduleConfiguration schedule,
        TimeZoneInfo timeZone,
        int slotIndex,
        int updatesPerDay)
    {
        var windowTicks = schedule.EndOfDay.Ticks - schedule.StartOfDay.Ticks;
        var slotStartTicks = schedule.StartOfDay.Ticks + GetSlotStartTicks(windowTicks, slotIndex, updatesPerDay);
        var localTime = TimeOnly.FromTimeSpan(TimeSpan.FromTicks(slotStartTicks));
        return ResolveLocalInstant(tradingDay, localTime, timeZone);
    }

    private static long GetSlotStartTicks(long windowTicks, int slotIndex, int updatesPerDay)
    {
        var quotient = windowTicks / updatesPerDay;
        var remainder = windowTicks % updatesPerDay;
        return quotient * slotIndex + (long)((decimal)remainder * slotIndex / updatesPerDay);
    }

    private static DateTime GetNextValidLocalTime(DateTime invalidLocalTime, TimeZoneInfo timeZone)
    {
        var invalidTicks = invalidLocalTime.Ticks;
        var validTicks = invalidTicks;
        do
        {
            validTicks = checked(validTicks + TimeSpan.TicksPerMinute);
        }
        while (timeZone.IsInvalidTime(new DateTime(validTicks, DateTimeKind.Unspecified)));

        var lowerBound = invalidTicks;
        while (validTicks - lowerBound > 1)
        {
            var candidateTicks = lowerBound + ((validTicks - lowerBound) / 2);
            if (timeZone.IsInvalidTime(new DateTime(candidateTicks, DateTimeKind.Unspecified)))
            {
                lowerBound = candidateTicks;
            }
            else
            {
                validTicks = candidateTicks;
            }
        }

        return new DateTime(validTicks, DateTimeKind.Unspecified);
    }

    private static bool IsValidFrequency(MarketCategoryInstrumentFrequency frequency) =>
        frequency.CurrentUpdatesPerDay >= 0
        && (frequency.PendingUpdatesPerDay is null || frequency.PendingUpdatesPerDay >= 0)
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

    private static string GetScheduleIdentity(
        TradingScheduleConfiguration schedule,
        MarketCategoryInstrumentFrequency frequency) =>
        string.Join(
            "|",
            GetScheduleIdentity(schedule),
            schedule.AppliedBrokerEnvironmentId?.ToString("N") ?? string.Empty,
            schedule.ScheduleVersion.ToString(CultureInfo.InvariantCulture),
            frequency.CurrentUpdatesPerDay.ToString(CultureInfo.InvariantCulture),
            frequency.LeadInMinutes.ToString(CultureInfo.InvariantCulture),
            frequency.ConfigurationVersion.ToString(CultureInfo.InvariantCulture));

    private static bool MatchesScheduleIdentity(
        string? previousIdentity,
        string currentIdentity,
        TradingScheduleConfiguration schedule) =>
        string.Equals(previousIdentity, currentIdentity, StringComparison.Ordinal)
        || string.Equals(
            previousIdentity,
            GetScheduleRevision(currentIdentity).ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal)
        || string.Equals(
            previousIdentity,
            GetScheduleIdentity(schedule),
            StringComparison.Ordinal)
        || string.Equals(
            previousIdentity,
            GetScheduleRevision(schedule).ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

    internal static long GetScheduleRevision(TradingScheduleConfiguration schedule) =>
        GetScheduleRevision(GetScheduleIdentity(schedule));

    internal static long GetScheduleRevision(
        TradingScheduleConfiguration schedule,
        MarketCategoryInstrumentFrequency frequency) =>
        GetScheduleRevision(GetScheduleIdentity(schedule, frequency));

    private static long GetScheduleRevision(string scheduleIdentity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(scheduleIdentity));
        return BitConverter.ToInt64(hash, 0) & long.MaxValue;
    }

    private static MarketCategoryInstrumentScheduleDecision Blocked(MarketCategoryInstrumentScheduleBlockReason reason) =>
        new(false, reason, null, null, null, null, []);
}
