using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;

namespace TNC.Trading.Platform.Api.Features.Platform;

internal static class MarketCategoryInstrumentEndpointMapping
{
    public static MarketCategoryInstrumentPageResponse ToResponse(
        this MarketCategoryInstrumentSnapshotPage page,
        string categoryCode,
        TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind environment,
        IDataProtector protector)
    {
        var nextCursor = page.NextEpic is null
            ? null
            : protector.Protect(JsonSerializer.Serialize(new InstrumentPageCursor(
                environment.ToString(),
                categoryCode,
                page.SnapshotVersion,
                page.NextEpic)));
        return new(
            "Complete",
            categoryCode,
            page.SnapshotVersion,
            page.LastRetrievedAtUtc,
            page.Instruments.Select(instrument => new MarketCategoryInstrumentResponse(
                instrument.Epic,
                instrument.InstrumentName,
                instrument.InstrumentType,
                instrument.UnderlyingName,
                instrument.Expiry,
                instrument.LotSize,
                instrument.OtcTradeable,
                instrument.ScalingFactor,
                instrument.ExpiryTimestamp,
                instrument.MarketStatus,
                instrument.DelayTime,
                instrument.Bid,
                instrument.Offer,
                instrument.High,
                instrument.Low,
                instrument.NetChange,
                instrument.PercentageChange,
                instrument.UpdateTime,
                instrument.Popularity)).ToArray(),
            nextCursor);
    }

    public static MarketCategoryInstrumentStatusResponse ToResponse(
        this GetMarketCategoryInstrumentStatusResponse response)
    {
        var status = response.CollectionStatus;
        return new(
            response.IsAvailable ? "Available" : "Paused",
            response.BrokerEnvironment?.ToString(),
            response.TradingDay,
            response.IsDue,
            response.CurrentSlot,
            response.NextWakeUpUtc,
            response.PauseReason,
            status?.LastCategoryRefreshAtUtc,
            status?.LastCompletedSlot,
            status?.CycleOutcome,
            status?.CategoryPrerequisiteOutcome,
            status?.SafeCategoryFailure,
            status?.UsedRequestBudget ?? 0,
            status?.ApprovedDailyRequestAllowance,
            status?.Categories.Select(category => new MarketCategoryCollectionStatusResponse(
                category.CategoryCode,
                category.LastSuccessfulCollectionAtUtc,
                category.Attempts,
                category.Outcome,
                category.SafeFailure)).ToArray() ?? []);
    }
}
