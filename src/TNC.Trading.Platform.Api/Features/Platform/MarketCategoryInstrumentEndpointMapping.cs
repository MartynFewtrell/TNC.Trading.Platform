using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Application.Features.MarketDetails;

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
        var availabilityByEpic = page.DetailAvailability?.ToDictionary(item => item.Epic, StringComparer.Ordinal)
            ?? new Dictionary<string, MarketDetailAvailability>(StringComparer.Ordinal);
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
                instrument.Popularity,
                availabilityByEpic.TryGetValue(instrument.Epic, out var availability)
                    ? availability.ToResponse()
                    : throw new InvalidOperationException("A listed instrument is missing its saved market-detail availability."))).ToArray(),
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
                category.SafeFailure,
                category.DetailCoverage?.ToResponse())).ToArray() ?? []);
    }

    public static MarketDetailResponse ToResponse(
        this GetMarketDetailResponse response)
    {
        var detail = response.Detail
            ?? throw new InvalidOperationException("A found market-detail response is missing its read result.");
        if (response.Status != GetMarketDetailStatus.Found)
        {
            throw new InvalidOperationException("Only a found market-detail result can be mapped to an API response.");
        }

        return new(
            detail.IsCategoryFollowed ? detail.TargetStatus.ToString() : "NotFollowed",
            response.CategoryCode,
            response.Epic,
            detail.ListingSnapshotVersion,
            detail.ListingRetrievedAtUtc,
            new(
                detail.IsCategoryFollowed,
                detail.AggregateStatus.ToString(),
                detail.Counts?.ExpectedCount,
                detail.Counts?.CompletedCount,
                detail.Counts?.ExcludedCount,
                detail.Counts?.OutstandingCount,
                detail.LastCompleteAtUtc,
                detail.NextScheduledCheckUtc,
                detail.SafeFailureCode),
            detail.Observation is { } observation
                ? new(
                    observation.RetrievedAtUtc,
                    observation.Source.ToString(),
                    observation.SourceEndpoint,
                    observation.SourceVersion,
                    observation.ProviderUpdateTimeText,
                    observation.Instrument.ToResponse(),
                    observation.DealingRules.ToResponse(),
                    observation.Snapshot.ToResponse())
                : null);
    }

    public static MarketDetailAvailabilityResponse ToResponse(this MarketDetailAvailability availability) =>
        availability.CurrentMembershipExists
            ? new(
            !availability.IsCategoryFollowed ? "NotFollowed" : availability.TargetStatus.ToString(),
            availability.DetailRetrievedAtUtc,
            availability.SafeFailureCode)
            : throw new InvalidOperationException("A listed instrument is missing its current category membership.");

    public static MarketDetailCoverageResponse ToResponse(this MarketDetailCategoryCoverage coverage) =>
        new(
            coverage.IsFollowed,
            !coverage.IsFollowed ? "NotFollowed" : coverage.AggregateStatus.ToString(),
            coverage.Counts?.ExpectedCount,
            coverage.Counts?.CompletedCount,
            coverage.Counts?.ExcludedCount,
            coverage.Counts?.OutstandingCount,
            coverage.LastCompleteAtUtc,
            coverage.NextScheduledCheckUtc,
            coverage.SafeFailureCode);

    private static MarketDetailInstrumentResponse ToResponse(this MarketDetailInstrument instrument) =>
        new(
            instrument.Epic,
            instrument.Expiry,
            instrument.Name,
            instrument.MarketId,
            instrument.Type,
            instrument.Unit,
            instrument.LotSize,
            instrument.ForceOpenAllowed,
            instrument.StopsLimitsAllowed,
            instrument.ControlledRiskAllowed,
            instrument.StreamingPricesAvailable,
            instrument.Currencies.Select(currency => new MarketDetailCurrencyResponse(
                currency.Code,
                currency.Symbol,
                currency.BaseExchangeRate,
                currency.ExchangeRate,
                currency.IsDefault)).ToArray(),
            instrument.MarginDepositBands.Select(band => new MarketDetailMarginDepositBandResponse(
                band.Minimum,
                band.Maximum.ToResponse(),
                band.Margin,
                band.Currency)).ToArray(),
            instrument.MarginFactor,
            instrument.MarginFactorUnit,
            instrument.SlippageFactor.ToResponse(),
            instrument.LimitedRiskPremium.ToResponse(),
            instrument.SprintMarketsMinimumExpiryTime.ToResponse(),
            instrument.SprintMarketsMaximumExpiryTime.ToResponse(),
            instrument.OpeningHoursJson,
            instrument.ExpiryDetailsJson,
            instrument.RolloverDetailsJson,
            instrument.NewsCode,
            instrument.ChartCode,
            instrument.Country,
            instrument.ValueOfOnePip,
            instrument.OnePipMeans,
            instrument.ContractSize,
            instrument.SpecialInfo);

    private static MarketDetailDealingRulesResponse ToResponse(this MarketDetailDealingRules rules) =>
        new(
            rules.ControlledRiskSpacing.ToResponse(),
            rules.MaxStopOrLimitDistance.ToResponse(),
            rules.MinControlledRiskStopDistance.ToResponse(),
            rules.MinDealSize.ToResponse(),
            rules.MinNormalStopOrLimitDistance.ToResponse(),
            rules.MinStepDistance.ToResponse(),
            rules.MarketOrderPreference,
            rules.TrailingStopsPreference);

    private static MarketDetailMarketSnapshotResponse ToResponse(this MarketDetailMarketSnapshot snapshot) =>
        new(
            snapshot.MarketStatus,
            snapshot.NetChange.ToResponse(),
            snapshot.PercentageChange.ToResponse(),
            snapshot.UpdateTimeText,
            snapshot.DelayTime.ToResponse(),
            snapshot.Bid.ToResponse(),
            snapshot.Offer.ToResponse(),
            snapshot.High.ToResponse(),
            snapshot.Low.ToResponse(),
            snapshot.BinaryOdds.ToResponse(),
            snapshot.DecimalPlacesFactor.ToResponse(),
            snapshot.ScalingFactor.ToResponse(),
            snapshot.ControlledRiskExtraSpread.ToResponse());

    private static MarketDetailQuantityResponse ToResponse(this MarketDetailQuantity quantity) =>
        new(quantity.Presence.ToString(), quantity.Value, quantity.Unit);
}
