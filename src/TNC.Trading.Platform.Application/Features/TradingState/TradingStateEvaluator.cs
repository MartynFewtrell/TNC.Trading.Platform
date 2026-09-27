using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.Features.TradingState;

internal sealed class TradingStateEvaluator(
    TradingScheduleGate tradingScheduleGate,
    MarketCategoryInstrumentSchedulePolicy schedulePolicy)
{
    public TradingStateEvaluation Evaluate(TradingStateEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var commonReasons = new List<TradingStateBlockReason>();
        var tradeBlockReasons = new List<TradingStateBlockReason>();
        var marketDataBlockReasons = new List<TradingStateBlockReason>();
        var schedule = request.Configuration.TradingSchedule;
        var frequency = request.CollectionFrequency;
        var applied = request.AppliedBrokerEnvironment;
        var hasMatchingAppliedEnvironment = applied is not null
            && schedule.AppliedBrokerEnvironmentId == applied.BrokerEnvironmentId
            && Enum.TryParse<BrokerEnvironmentKind>(applied.Kind, true, out var appliedEnvironment)
            && appliedEnvironment == request.Configuration.BrokerEnvironment;
        var hasValidSchedule = schedule.StartOfDay < schedule.EndOfDay
            && schedule.TradingDays.Count > 0
            && schedule.ScheduleVersion > 0
            && TradingScheduleGate.TryResolveTimeZone(schedule.TimeZone, out _);
        var tradingDay = hasValidSchedule
            ? tradingScheduleGate.GetTradingDay(schedule, request.NowUtc)
            : (DateOnly?)null;
        var scheduleStatus = hasValidSchedule
            ? tradingScheduleGate.Evaluate(schedule, request.NowUtc)
            : new TradingScheduleStatus(false, "Trading schedule is invalid.");
        var tradingWindowOpen = applied is not null
            && applied.IsExecutable
            && hasMatchingAppliedEnvironment
            && hasValidSchedule
            && scheduleStatus.IsActive;

        if (applied is null || !applied.IsExecutable)
        {
            commonReasons.Add(TradingStateBlockReason.AppliedBrokerUnavailable);
        }
        else if (!hasMatchingAppliedEnvironment)
        {
            commonReasons.Add(TradingStateBlockReason.AppliedBrokerMismatch);
        }

        if (!hasValidSchedule)
        {
            commonReasons.Add(TradingStateBlockReason.InvalidTradingSchedule);
        }
        else if (!scheduleStatus.IsActive)
        {
            commonReasons.Add(TradingStateBlockReason.TradingWindowClosed);
        }

        if (request.RuntimeState?.SessionStatus != PlatformSessionStatus.Active)
        {
            tradeBlockReasons.Add(TradingStateBlockReason.TradingReadinessUnavailable);
        }

        if (TradingScheduleGate.IsLiveTargetBlocked(
            request.Configuration.PlatformEnvironment,
            request.Configuration.BrokerEnvironment))
        {
            tradeBlockReasons.Add(TradingStateBlockReason.TestEnvironmentCannotTargetLiveBroker);
        }

        if (applied is null
            || !applied.IsExecutable
            || !applied.CanAccessMarketData
            || !string.Equals(applied.Provider, "IG", StringComparison.OrdinalIgnoreCase)
            || !hasMatchingAppliedEnvironment)
        {
            marketDataBlockReasons.Add(TradingStateBlockReason.MarketDataUnavailable);
        }

        if (request.Configuration.MarketDataScheduleReconciliationRequired)
        {
            marketDataBlockReasons.Add(TradingStateBlockReason.ScheduleReconciliationRequired);
        }

        if (frequency.ApprovedNonTradingDailyRequestAllowance is not > 0)
        {
            marketDataBlockReasons.Add(TradingStateBlockReason.RequestAllowanceNotApproved);
        }

        if (frequency.CurrentUpdatesPerDay < 0
            || frequency.LeadInMinutes < 0
            || frequency.ConfigurationVersion < 1)
        {
            marketDataBlockReasons.Add(TradingStateBlockReason.CollectionSettingsInvalid);
        }

        var canTrade = tradingWindowOpen
            && applied?.IsExecutable == true
            && hasMatchingAppliedEnvironment
            && request.RuntimeState?.SessionStatus == PlatformSessionStatus.Active
            && !TradingScheduleGate.IsLiveTargetBlocked(
                request.Configuration.PlatformEnvironment,
                request.Configuration.BrokerEnvironment);
        var canStartMarketDataUpdate = tradingWindowOpen
            && applied?.IsExecutable == true
            && applied.CanAccessMarketData
            && string.Equals(applied.Provider, "IG", StringComparison.OrdinalIgnoreCase)
            && hasMatchingAppliedEnvironment
            && !request.Configuration.MarketDataScheduleReconciliationRequired
            && frequency.ApprovedNonTradingDailyRequestAllowance is > 0
            && frequency.CurrentUpdatesPerDay >= 0
            && frequency.LeadInMinutes >= 0
            && frequency.ConfigurationVersion >= 1;

        var nextWindowOpeningUtc = hasValidSchedule
            ? schedulePolicy.GetNextWindowOpeningUtc(schedule)
            : null;
        var nextWindowClosingUtc = hasValidSchedule
            ? schedulePolicy.GetNextWindowClosingUtc(schedule)
            : null;
        var nextScheduledStartUtc = hasValidSchedule
            ? schedulePolicy.GetNextScheduledStartUtc(schedule, frequency.CurrentUpdatesPerDay)
            : null;

        return new(
            tradingWindowOpen,
            canTrade,
            canStartMarketDataUpdate,
            applied?.BrokerEnvironmentId,
            hasValidSchedule ? MarketCategoryInstrumentSchedulePolicy.GetScheduleRevision(schedule, frequency) : 0,
            frequency.ConfigurationVersion,
            tradingDay,
            nextWindowOpeningUtc,
            nextWindowClosingUtc,
            nextScheduledStartUtc,
            commonReasons.Concat(tradeBlockReasons).Distinct().ToArray(),
            commonReasons.Concat(marketDataBlockReasons).Distinct().ToArray());
    }
}
