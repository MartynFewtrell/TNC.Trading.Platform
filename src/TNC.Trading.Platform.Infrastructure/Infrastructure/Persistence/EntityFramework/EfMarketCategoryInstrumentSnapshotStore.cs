using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketCategoryInstruments;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketCategoryInstrumentSnapshotStore(
    PlatformDbContext dbContext,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null,
    TimeProvider? timeProvider = null) :
    IMarketCategoryInstrumentSnapshotReader,
    IMarketCategoryInstrumentSnapshotWriter
{
    private const int MaximumPageSize = 150;
    private const int MaximumPages = 100;
    private const int MaximumResults = 15_000;

    public async Task<MarketCategoryInstrumentSnapshot> SaveCompleteAsync(
        MarketCategoryInstrumentCollection collection,
        MarketCategoryInstrumentRunProvenance provenance,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SaveCompleteCoreAsync(collection, provenance, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<MarketCategoryInstrumentSnapshot> SaveCompleteCoreAsync(
        MarketCategoryInstrumentCollection collection,
        MarketCategoryInstrumentRunProvenance provenance,
        CancellationToken cancellationToken)
    {
        ValidateCompleteCollection(collection, provenance);
        var startTimeUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().ToUniversalTime();
        if (provenance.ScheduleWindowEndUtc is { } windowEnd && startTimeUtc >= windowEnd)
        {
            throw new InvalidOperationException("The Trading window closed before instrument snapshot publication.");
        }

        var environmentId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext,
            contextResolver,
            collection.BrokerEnvironment,
            cancellationToken).ConfigureAwait(false);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext, $"MarketCategoryInstruments/{environmentId:N}", "Shared", cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext, $"MarketCategoryInstruments/{environmentId:N}/{collection.CategoryCode}", "Exclusive", cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext, $"InstrumentCycle/{environmentId:N}/{provenance.TradingDay:yyyyMMdd}/{provenance.ScheduledSlot}", "Exclusive", cancellationToken).ConfigureAwait(false);
        var categoryExists = await dbContext.MarketCategories
            .AnyAsync(item => item.BrokerEnvironmentId == environmentId && item.Code == collection.CategoryCode, cancellationToken)
            .ConfigureAwait(false);
        if (!categoryExists)
        {
            throw new InvalidOperationException("The requested category is not in the applied environment's current catalogue.");
        }

        var categoryCatalogState = await dbContext.MarketCategoryCatalogStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The current category catalogue has no saved revision.");
        if (provenance.CategorySnapshotRevision != categoryCatalogState.Revision)
        {
            throw new InvalidOperationException("The category catalogue changed while instruments were being collected.");
        }
        var cycle = await dbContext.InstrumentCollectionCycleStates.SingleOrDefaultAsync(
            item => item.BrokerEnvironmentId == environmentId
                && item.TradingDay == provenance.TradingDay
                && item.ScheduledSlot == provenance.ScheduledSlot,
            cancellationToken).ConfigureAwait(false);
        var attempt = await dbContext.InstrumentCollectionCategoryAttempts.SingleOrDefaultAsync(
            item => item.BrokerEnvironmentId == environmentId
                && item.TradingDay == provenance.TradingDay
                && item.ScheduledSlot == provenance.ScheduledSlot
                && item.CategoryCode == collection.CategoryCode,
            cancellationToken).ConfigureAwait(false);
        var commitTimeUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().ToUniversalTime();
        if (cycle is null
            || cycle.LeaseOwner != provenance.LeaseOwner
            || cycle.LeaseFence != provenance.LeaseFence
            || cycle.LeaseExpiresAtUtc <= commitTimeUtc
            || cycle.CategoryPrerequisite != "Succeeded"
            || cycle.Outcome != "Running"
            || attempt is null
            || attempt.State != "Running"
            || attempt.LeaseFence != provenance.LeaseFence)
        {
            throw new InvalidOperationException("The instrument collection lease or category-slot reservation is stale.");
        }

        var state = await dbContext.MarketCategoryInstrumentCatalogStates
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId && item.CategoryCode == collection.CategoryCode, cancellationToken)
            .ConfigureAwait(false);
        var lastRunVersion = await dbContext.MarketCategoryInstrumentCollectionRuns
            .Where(item => item.BrokerEnvironmentId == environmentId && item.CategoryCode == collection.CategoryCode)
            .MaxAsync(item => (long?)item.SnapshotVersion, cancellationToken).ConfigureAwait(false) ?? 0;
        var nextVersion = checked(Math.Max(state?.SnapshotVersion ?? 0, lastRunVersion) + 1);

        var run = new MarketCategoryInstrumentCollectionRunEntity
        {
            CollectionId = provenance.RunId,
            BrokerEnvironmentId = environmentId,
            EndpointProfile = provenance.AppliedEndpointProfile,
            CategoryCode = provenance.CategoryCode,
            CategorySnapshotRevision = provenance.CategorySnapshotRevision,
            SnapshotVersion = nextVersion,
            TradingDay = provenance.TradingDay,
            ScheduledSlot = provenance.ScheduledSlot,
            EffectiveUpdatesPerDay = provenance.EffectiveUpdatesPerDay,
            RetrievedAtUtc = provenance.RetrievedAtUtc,
            PageSize = collection.Metadata.PageSize,
            PageCount = collection.Metadata.PageNumbersFetched.Count,
            ProviderTotalPages = collection.Metadata.ProviderTotalPages,
            ProviderTotalResults = collection.Metadata.ProviderTotalResults,
            ResultCount = collection.Instruments.Count,
            QualityStatus = provenance.DataQualityEvidence.Status.ToString(),
            MissingOptionalValueCount = provenance.DataQualityEvidence.MissingOptionalValueCount,
            IsComplete = true
        };
        foreach (var instrument in collection.Instruments)
        {
            run.Observations.Add(ToObservation(environmentId, collection.CategoryCode, provenance, instrument));
        }

        dbContext.MarketCategoryInstrumentCollectionRuns.Add(run);

        var oldRows = await dbContext.MarketCategoryInstruments
            .Where(item => item.BrokerEnvironmentId == environmentId && item.CategoryCode == collection.CategoryCode)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        dbContext.MarketCategoryInstruments.RemoveRange(oldRows);
        dbContext.MarketCategoryInstruments.AddRange(collection.Instruments.Select(instrument =>
            ToCurrent(environmentId, collection.CategoryCode, nextVersion, provenance.RunId, instrument)));

        if (state is null)
        {
            state = new MarketCategoryInstrumentCatalogStateEntity
            {
                BrokerEnvironmentId = environmentId,
                CategoryCode = collection.CategoryCode
            };
            dbContext.MarketCategoryInstrumentCatalogStates.Add(state);
        }

        state.SnapshotVersion = nextVersion;
        state.CollectionId = provenance.RunId;
        state.LastRefreshedAtUtc = provenance.RetrievedAtUtc;
        attempt.State = "Succeeded";
        attempt.SafeError = null;
        attempt.UpdatedAtUtc = commitTimeUtc;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var categoryStillPresent = await dbContext.MarketCategories.AsNoTracking()
            .AnyAsync(item => item.BrokerEnvironmentId == environmentId && item.Code == collection.CategoryCode, cancellationToken)
            .ConfigureAwait(false);
        var finalCategoryRevision = await dbContext.MarketCategoryCatalogStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => (long?)item.Revision)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (!categoryStillPresent || finalCategoryRevision != categoryCatalogState.Revision)
        {
            throw new InvalidOperationException("The current category membership or catalogue revision changed before collection publication.");
        }

        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            environmentId,
            collection.BrokerEnvironment,
            provenance.AppliedEndpointProfile,
            cancellationToken).ConfigureAwait(false);
        if (provenance.ScheduleWindowEndUtc is { } finalWindowEnd
            && ((timeProvider ?? TimeProvider.System).GetUtcNow().ToUniversalTime() >= finalWindowEnd
                || cancellationToken.IsCancellationRequested))
        {
            throw new InvalidOperationException("The Trading window closed before instrument snapshot commit.");
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(nextVersion, provenance, collection.Instruments);
    }

    public async Task<MarketCategoryInstrumentSnapshotPage?> ReadPageAsync(
        MarketCategoryInstrumentSnapshotPageRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PageSize is < 1 or > 100
            || string.IsNullOrWhiteSpace(request.CategoryCode)
            || request.CategoryCode.Length > 128
            || request.SnapshotVersion is < 1
            || request.AfterEpic is { Length: 0 or > 64 })
        {
            throw new ArgumentOutOfRangeException(nameof(request), "A valid category, snapshot version and page size between 1 and 100 are required.");
        }

        var environmentId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext,
            contextResolver,
            request.BrokerEnvironment,
            cancellationToken,
            requireExecutable: false).ConfigureAwait(false);
        var state = await dbContext.MarketCategoryInstrumentCatalogStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId && item.CategoryCode == request.CategoryCode, cancellationToken)
            .ConfigureAwait(false);
        if (state is null
            || request.SnapshotVersion is { } requestedVersion && state.SnapshotVersion != requestedVersion)
        {
            return null;
        }

        var query = dbContext.MarketCategoryInstruments.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId && item.CategoryCode == request.CategoryCode);
        if (request.AfterEpic is not null)
        {
            query = query.Where(item => item.Epic.CompareTo(request.AfterEpic) > 0);
        }

        var rows = await query.OrderBy(item => item.Epic)
            .Take(request.PageSize + 1)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var currentState = await dbContext.MarketCategoryInstrumentCatalogStates.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.BrokerEnvironmentId == environmentId && item.CategoryCode == request.CategoryCode,
                cancellationToken)
            .ConfigureAwait(false);
        if (currentState is null || currentState.SnapshotVersion != state.SnapshotVersion)
        {
            return null;
        }

        var hasMore = rows.Count > request.PageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return new(
            state.SnapshotVersion,
            state.LastRefreshedAtUtc,
            rows.Select(ToValue).ToArray(),
            hasMore ? rows[^1].Epic : null);
    }

    private static void ValidateCompleteCollection(
        MarketCategoryInstrumentCollection collection,
        MarketCategoryInstrumentRunProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(provenance);
        var metadata = collection.Metadata;
        if (provenance.RunId == Guid.Empty
            || provenance.BrokerEnvironment != collection.BrokerEnvironment
            || !string.Equals(provenance.CategoryCode, collection.CategoryCode, StringComparison.Ordinal)
            || provenance.CategorySnapshotRevision < 1
            || string.IsNullOrWhiteSpace(collection.CategoryCode)
            || collection.CategoryCode.Length > 128
            || string.IsNullOrWhiteSpace(provenance.AppliedEndpointProfile)
            || provenance.AppliedEndpointProfile.Length > 128
            || provenance.ScheduledSlot < 0
            || provenance.EffectiveUpdatesPerDay is < 1 or > 4
            || provenance.RetrievedAtUtc.Offset != TimeSpan.Zero
            || provenance.LeaseOwner == Guid.Empty
            || provenance.LeaseFence < 1
            || metadata.PageSize is < 1 or > MaximumPageSize
            || metadata.ProviderTotalPages is < 1 or > MaximumPages
            || metadata.ProviderTotalResults is < 1 or > MaximumResults
            || collection.Instruments.Count is < 1 or > MaximumResults
            || metadata.ProviderTotalPages != metadata.PageNumbersFetched.Count
            || metadata.ProviderTotalResults != collection.Instruments.Count
            || metadata.PageNumbersFetched.Where((page, index) => page != index + 1).Any()
            || metadata.ProviderTotalPages != (int)Math.Ceiling((double)metadata.ProviderTotalResults / metadata.PageSize))
        {
            throw new InvalidOperationException("The instrument collection is incomplete or has invalid provenance.");
        }

        var seenEpics = new HashSet<string>(StringComparer.Ordinal);
        var missingOptionalValues = 0;
        foreach (var instrument in collection.Instruments)
        {
            if (string.IsNullOrWhiteSpace(instrument.Epic)
                || instrument.Epic.Length > 64
                || string.IsNullOrWhiteSpace(instrument.InstrumentName)
                || instrument.InstrumentName.Length > 256
                || instrument.InstrumentType?.Length > 64
                || instrument.UnderlyingName?.Length > 256
                || instrument.Expiry?.Length > 32
                || instrument.MarketStatus?.Length > 32
                || instrument.UpdateTime?.Length > 32
                || !seenEpics.Add(instrument.Epic))
            {
                throw new InvalidOperationException("An instrument identity or provider field is invalid or duplicated.");
            }

            if (instrument.ExpiryTimestamp is not null)
            {
                try
                {
                    _ = DateTimeOffset.FromUnixTimeMilliseconds(instrument.ExpiryTimestamp.Value);
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    throw new InvalidOperationException("An instrument expiry timestamp is outside the supported range.", exception);
                }
            }

            missingOptionalValues += CountMissingOptionalValues(instrument);
        }

        var evidence = provenance.DataQualityEvidence;
        var expectedStatus = missingOptionalValues == 0
            ? MarketCategoryInstrumentDataQualityStatus.CompleteValidated
            : MarketCategoryInstrumentDataQualityStatus.CompleteValidatedWithOptionalValuesMissing;
        if (evidence.ValidatedInstrumentCount != collection.Instruments.Count
            || evidence.MissingOptionalValueCount != missingOptionalValues
            || evidence.Status != expectedStatus)
        {
            throw new InvalidOperationException("The collection quality evidence does not match its validated observations.");
        }
    }

    private static int CountMissingOptionalValues(MarketCategoryInstrument value) =>
        new object?[]
        {
            value.InstrumentType, value.UnderlyingName, value.Expiry, value.LotSize, value.OtcTradeable,
            value.ScalingFactor, value.ExpiryTimestamp, value.MarketStatus, value.DelayTime, value.Bid,
            value.Offer, value.High, value.Low, value.NetChange, value.PercentageChange, value.UpdateTime,
            value.Popularity
        }.Count(item => item is null);

    private static MarketCategoryInstrumentObservationEntity ToObservation(
        Guid environmentId,
        string categoryCode,
        MarketCategoryInstrumentRunProvenance provenance,
        MarketCategoryInstrument value) =>
        new()
        {
            CollectionId = provenance.RunId,
            Epic = value.Epic,
            BrokerEnvironmentId = environmentId,
            CategoryCode = categoryCode,
            RetrievedAtUtc = provenance.RetrievedAtUtc,
            InstrumentName = value.InstrumentName,
            InstrumentType = value.InstrumentType,
            UnderlyingName = value.UnderlyingName,
            Expiry = value.Expiry,
            LotSize = value.LotSize,
            OtcTradeable = value.OtcTradeable,
            ScalingFactor = value.ScalingFactor,
            ExpiryTimestamp = value.ExpiryTimestamp,
            MarketStatus = value.MarketStatus,
            DelayTime = value.DelayTime,
            Bid = value.Bid,
            Offer = value.Offer,
            High = value.High,
            Low = value.Low,
            NetChange = value.NetChange,
            PercentageChange = value.PercentageChange,
            UpdateTime = value.UpdateTime,
            Popularity = value.Popularity
        };

    private static MarketCategoryInstrumentEntity ToCurrent(
        Guid environmentId,
        string categoryCode,
        long version,
        Guid collectionId,
        MarketCategoryInstrument value) =>
        new()
        {
            BrokerEnvironmentId = environmentId,
            CategoryCode = categoryCode,
            Epic = value.Epic,
            SnapshotVersion = version,
            CollectionId = collectionId,
            InstrumentName = value.InstrumentName,
            InstrumentType = value.InstrumentType,
            UnderlyingName = value.UnderlyingName,
            Expiry = value.Expiry,
            LotSize = value.LotSize,
            OtcTradeable = value.OtcTradeable,
            ScalingFactor = value.ScalingFactor,
            ExpiryTimestamp = value.ExpiryTimestamp,
            MarketStatus = value.MarketStatus,
            DelayTime = value.DelayTime,
            Bid = value.Bid,
            Offer = value.Offer,
            High = value.High,
            Low = value.Low,
            NetChange = value.NetChange,
            PercentageChange = value.PercentageChange,
            UpdateTime = value.UpdateTime,
            Popularity = value.Popularity
        };

    private static MarketCategoryInstrument ToValue(MarketCategoryInstrumentEntity value) =>
        new(value.Epic, value.InstrumentName, value.InstrumentType, value.UnderlyingName, value.Expiry, value.LotSize,
            value.OtcTradeable, value.ScalingFactor, value.ExpiryTimestamp, value.MarketStatus, value.DelayTime, value.Bid,
            value.Offer, value.High, value.Low, value.NetChange, value.PercentageChange, value.UpdateTime, value.Popularity);
}
