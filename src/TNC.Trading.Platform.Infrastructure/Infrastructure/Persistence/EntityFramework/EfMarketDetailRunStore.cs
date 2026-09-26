using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDetails;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketDetailRunStore(
    PlatformDbContext dbContext,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null,
    TimeProvider? timeProvider = null) :
    IMarketDetailRunStore,
    IMarketDetailObservationWriter
{
    private const int MaximumBatchSize = 500;
    private const int MaximumOutstandingBatchSize = 50;
    private const int MaximumCapacityTargets = 15_000;
    private const int MaximumTargetAttempts = 3;
    private const int DetailSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<MarketDetailRunLease?> TryAcquireAsync(
        MarketDetailRunKey key,
        MarketDetailRevisions revisions,
        string appliedEndpointProfile,
        Guid owner,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        DateTimeOffset windowEndUtc,
        CancellationToken cancellationToken)
    {
        ValidateAcquireArguments(key, revisions, appliedEndpointProfile, owner, nowUtc, leaseDuration, windowEndUtc);
        var environmentId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext,
            contextResolver,
            key.Environment,
            cancellationToken).ConfigureAwait(false);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await AcquireRunLockAsync(environmentId, key, cancellationToken).ConfigureAwait(false);
        var appliedEnvironment = await dbContext.BrokerEnvironments.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => new { item.EndpointProfile })
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(appliedEnvironment.EndpointProfile, appliedEndpointProfile, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The applied endpoint profile changed before the market-detail lease was acquired.");
        }

        var run = await dbContext.MarketDetailCollectionRuns.SingleOrDefaultAsync(
            item => item.BrokerEnvironmentId == environmentId
                && item.TradingDay == key.TradingDay
                && item.ScheduledSlot == key.SlotIndex,
            cancellationToken).ConfigureAwait(false);
        if (run is not null
            && (!string.Equals(run.EndpointProfile, appliedEndpointProfile, StringComparison.Ordinal)
                || run.CatalogueRevision != revisions.CatalogueRevision
                || run.InterestRevision != revisions.InterestRevision
                || run.ScheduleRevision != revisions.ScheduleRevision
                || run.WindowEndUtc != windowEndUtc))
        {
            run.Status = "Superseded";
            run.SafeReasonCode = "RunProvenanceChanged";
            ReleaseLease(run, nowUtc);
            run.UpdatedAtUtc = nowUtc;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var expiresAtUtc = Min(nowUtc + leaseDuration, windowEndUtc);
        if (run is null)
        {
            run = new MarketDetailCollectionRunEntity
            {
                RunId = Guid.NewGuid(),
                BrokerEnvironmentId = environmentId,
                EndpointProfile = appliedEndpointProfile,
                TradingDay = key.TradingDay,
                ScheduledSlot = key.SlotIndex,
                CatalogueRevision = revisions.CatalogueRevision,
                InterestRevision = revisions.InterestRevision,
                ScheduleRevision = revisions.ScheduleRevision,
                Status = "Collecting",
                WindowEndUtc = windowEndUtc,
                LeaseOwner = owner,
                LeaseFence = 1,
                LeaseExpiresAtUtc = expiresAtUtc,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };
            dbContext.MarketDetailCollectionRuns.Add(run);
        }
        else
        {
            if (run.Status is "Complete" or "Superseded"
                || run.WindowEndUtc <= nowUtc
                || run.LeaseExpiresAtUtc > nowUtc)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            run.Status = "Collecting";
            run.SafeReasonCode = null;
            run.LeaseOwner = owner;
            run.LeaseFence = checked(run.LeaseFence + 1);
            run.LeaseExpiresAtUtc = expiresAtUtc;
            run.UpdatedAtUtc = nowUtc;
        }

        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            environmentId,
            key.Environment,
            appliedEndpointProfile,
            cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ToLease(run, key.Environment);
    }

    public async Task<MarketDetailRunStatus> StageUniverseAsync(
        MarketDetailRunLease lease,
        MarketDetailUniverse universe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(universe);
        if (!universe.IsReady)
        {
            await MarkBlockedAsync(lease, universe.BlockReason?.ToString() ?? "InvalidUniverse", cancellationToken).ConfigureAwait(false);
            return MarketDetailRunStatus.Blocked;
        }

        var environmentId = await ResolveAndValidateLeaseEnvironmentAsync(lease, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await AcquireRunLockAsync(environmentId, lease.Key, cancellationToken).ConfigureAwait(false);
        var run = await LoadOwnedRunAsync(lease, environmentId, cancellationToken).ConfigureAwait(false);

        if (run.IsUniverseStaged)
        {
            var matches = await VerifyStagedUniverseMatchesAsync(run, universe, cancellationToken).ConfigureAwait(false);
            if (!matches)
            {
                var nowUtc = clock.GetUtcNow().ToUniversalTime();
                run.Status = "Superseded";
                run.SafeReasonCode = "ListingSourcesChanged";
                ReleaseLease(run, nowUtc);
                run.UpdatedAtUtc = nowUtc;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return matches ? MarketDetailRunStatus.Running : MarketDetailRunStatus.Superseded;
        }

        var validationFailure = await ValidateUniverseSourcesAsync(
            environmentId,
            lease,
            universe,
            cancellationToken).ConfigureAwait(false);
        if (validationFailure is not null)
        {
            run.Status = "Blocked";
            run.SafeReasonCode = validationFailure;
            ReleaseLease(run, clock.GetUtcNow().ToUniversalTime());
            run.UpdatedAtUtc = clock.GetUtcNow().ToUniversalTime();
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return MarketDetailRunStatus.Blocked;
        }

        var sources = universe.Sources.Select(source => new MarketDetailRunSourceEntity
        {
            RunId = run.RunId,
            BrokerEnvironmentId = environmentId,
            CategoryCode = source.CategoryCode,
            ListingCollectionId = source.CollectionId,
            ListingVersion = source.Version,
            IsValidatedComplete = source.IsValidatedComplete
        }).ToArray();
        var targets = universe.Targets.Select(target => new MarketDetailRunTargetEntity
        {
            RunId = run.RunId,
            BrokerEnvironmentId = environmentId,
            Epic = target.Epic,
            Status = "Pending",
            UpdatedAtUtc = clock.GetUtcNow().ToUniversalTime()
        }).ToArray();
        var memberships = universe.Targets
            .SelectMany(target => target.Memberships.Select(membership => new MarketDetailRunMembershipEntity
            {
                RunId = run.RunId,
                BrokerEnvironmentId = environmentId,
                Epic = target.Epic,
                CategoryCode = membership.CategoryCode
            }))
            .ToArray();

        ValidateUniverseShape(universe, memberships);
        await InsertBatchesAsync(sources, cancellationToken).ConfigureAwait(false);
        await InsertBatchesAsync(targets, cancellationToken).ConfigureAwait(false);
        await InsertBatchesAsync(memberships, cancellationToken).ConfigureAwait(false);

        run.ExpectedCount = targets.Length;
        run.CompletedCount = 0;
        run.ExcludedCount = 0;
        run.IsUniverseStaged = true;
        run.UpdatedAtUtc = clock.GetUtcNow().ToUniversalTime();
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            environmentId,
            lease.Key.Environment,
            lease.AppliedEndpointProfile,
            cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return MarketDetailRunStatus.Running;
    }

    public async Task<IReadOnlyList<MarketDetailTarget>> ReadOutstandingTargetsAsync(
        MarketDetailRunLease lease,
        int maximumCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (maximumCount is < 1 or > MaximumOutstandingBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        var environmentId = await ResolveAndValidateLeaseEnvironmentAsync(lease, cancellationToken).ConfigureAwait(false);
        await ValidateOwnedRunForReadAsync(lease, environmentId, cancellationToken).ConfigureAwait(false);
        var rows = await dbContext.MarketDetailRunTargets.AsNoTracking()
            .Where(item => item.RunId == lease.RunId
                && item.BrokerEnvironmentId == environmentId
                && (item.Status == "Pending" || item.Status == "Failed")
                && item.Attempts < MaximumTargetAttempts)
            .OrderBy(item => item.Epic)
            .Take(maximumCount)
            .Select(item => new
            {
                item.Epic,
                item.Attempts,
                item.SafeFailureCode,
                Memberships = item.Memberships
                    .OrderBy(membership => membership.CategoryCode)
                    .Select(membership => new MarketDetailRunMembership(
                        membership.CategoryCode,
                        membership.Source.ListingCollectionId,
                        membership.Source.ListingVersion))
                    .ToArray()
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(item => new MarketDetailTarget(
            item.Epic,
            item.Memberships,
            item.Attempts,
            item.SafeFailureCode)).ToArray();
    }

    public async Task<IReadOnlyList<MarketDetailCapacityTarget>> ReadRetryableCapacityTargetsAsync(
        MarketDetailRunLease lease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var environmentId = await ResolveAndValidateLeaseEnvironmentAsync(lease, cancellationToken).ConfigureAwait(false);
        await ValidateOwnedRunForReadAsync(lease, environmentId, cancellationToken).ConfigureAwait(false);
        var rows = await dbContext.MarketDetailRunTargets.AsNoTracking()
            .Where(item => item.RunId == lease.RunId
                && item.BrokerEnvironmentId == environmentId
                && (item.Status == "Pending" || item.Status == "Failed")
                && item.Attempts < MaximumTargetAttempts)
            .OrderBy(item => item.Epic)
            .Take(MaximumCapacityTargets + 1)
            .Select(item => new MarketDetailCapacityTarget(item.Epic, item.Attempts))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Length > MaximumCapacityTargets)
        {
            throw new InvalidOperationException("The market-detail run exceeds the bounded capacity-estimation target count.");
        }

        return rows;
    }

    public async Task<bool> RecordFailureAsync(
        MarketDetailRunLease lease,
        MarketDetailGatewayResult result,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(result);
        if (result.Failure is null || failedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A failed gateway result and a UTC failure time are required.", nameof(result));
        }

        var environmentId = await ResolveAndValidateLeaseEnvironmentAsync(lease, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await AcquireRunLockAsync(environmentId, lease.Key, cancellationToken).ConfigureAwait(false);
        var run = await LoadOwnedRunAsync(lease, environmentId, cancellationToken).ConfigureAwait(false);
        var target = await dbContext.MarketDetailRunTargets.SingleOrDefaultAsync(
            item => item.RunId == lease.RunId && item.BrokerEnvironmentId == environmentId && item.Epic == result.Epic,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The failed EPIC is not in this frozen market-detail run.");
        if (target.Status == "Completed")
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (!await SupersedeIfSourcesChangedAsync(
                lease, environmentId, run, cancellationToken).ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var failure = result.Failure;
        var excludesTarget = new MarketDetailCollectionStatePolicy().ShouldExcludeTarget(
            failure.Kind,
            failure.IdentifiesEpic,
            positiveProviderEvidence: false);
        target.Attempts = checked(target.Attempts + 1);
        target.Status = excludesTarget ? "Excluded" : "Failed";
        target.SafeFailureCode = failure.Kind.ToString();
        target.ExclusionEvidenceCode = excludesTarget ? failure.Kind.ToString() : null;
        target.ExcludedAtUtc = excludesTarget ? failedAtUtc : null;
        target.UpdatedAtUtc = failedAtUtc;
        if (excludesTarget)
        {
            await MarkEligibilityExcludedAsync(environmentId, result.Epic, failure.Kind.ToString(), failedAtUtc, cancellationToken).ConfigureAwait(false);
        }

        run.UpdatedAtUtc = failedAtUtc;
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            environmentId,
            lease.Key.Environment,
            lease.AppliedEndpointProfile,
            cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<MarketDetailRunCounts> ReadCountsAsync(
        MarketDetailRunLease lease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var environmentId = await ResolveAndValidateLeaseEnvironmentAsync(lease, cancellationToken).ConfigureAwait(false);
        await ValidateOwnedRunForReadAsync(lease, environmentId, cancellationToken).ConfigureAwait(false);
        var total = await dbContext.MarketDetailRunTargets.CountAsync(
            item => item.RunId == lease.RunId && item.BrokerEnvironmentId == environmentId,
            cancellationToken).ConfigureAwait(false);
        var completed = await dbContext.MarketDetailRunTargets.CountAsync(
            item => item.RunId == lease.RunId && item.BrokerEnvironmentId == environmentId && item.Status == "Completed",
            cancellationToken).ConfigureAwait(false);
        var excluded = await dbContext.MarketDetailRunTargets.CountAsync(
            item => item.RunId == lease.RunId && item.BrokerEnvironmentId == environmentId && item.Status == "Excluded",
            cancellationToken).ConfigureAwait(false);
        return new(total - excluded, completed, excluded);
    }

    public async Task<MarketDetailRunStatus> FinalizeAsync(
        MarketDetailRunLease lease,
        bool prerequisitesValidated,
        bool revisionsCurrent,
        bool capacityAvailable,
        bool isRunning,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var environmentId = await ResolveAndValidateLeaseEnvironmentAsync(lease, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await AcquireRunLockAsync(environmentId, lease.Key, cancellationToken).ConfigureAwait(false);
        var run = await LoadOwnedRunAsync(lease, environmentId, cancellationToken).ConfigureAwait(false);
        var total = await dbContext.MarketDetailRunTargets.CountAsync(
            item => item.RunId == lease.RunId && item.BrokerEnvironmentId == environmentId,
            cancellationToken).ConfigureAwait(false);
        var completed = await dbContext.MarketDetailRunTargets.CountAsync(
            item => item.RunId == lease.RunId && item.BrokerEnvironmentId == environmentId && item.Status == "Completed",
            cancellationToken).ConfigureAwait(false);
        var excluded = await dbContext.MarketDetailRunTargets.CountAsync(
            item => item.RunId == lease.RunId && item.BrokerEnvironmentId == environmentId && item.Status == "Excluded",
            cancellationToken).ConfigureAwait(false);
        var sourcesValidated = run.IsUniverseStaged
            && await dbContext.MarketDetailRunSources
                .Where(item => item.RunId == lease.RunId && item.BrokerEnvironmentId == environmentId)
                .AllAsync(item => item.IsValidatedComplete, cancellationToken)
                .ConfigureAwait(false);
        var counts = new MarketDetailRunCounts(total - excluded, completed, excluded);
        var resolvedStatus = new MarketDetailCollectionStatePolicy().ResolveRunStatus(
            runExists: true,
            isRunning,
            prerequisitesValidated && sourcesValidated,
            revisionsCurrent,
            capacityAvailable,
            counts);
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        run.ExpectedCount = counts.ExpectedCount;
        run.CompletedCount = counts.CompletedCount;
        run.ExcludedCount = counts.ExcludedCount;
        run.Status = ToStoredStatus(resolvedStatus);
        if (resolvedStatus is MarketDetailRunStatus.Complete or MarketDetailRunStatus.Blocked or MarketDetailRunStatus.Superseded or MarketDetailRunStatus.Incomplete)
        {
            ReleaseLease(run, nowUtc);
        }

        run.SafeReasonCode = resolvedStatus switch
        {
            MarketDetailRunStatus.Blocked when !capacityAvailable => "CapacityUnavailable",
            MarketDetailRunStatus.Blocked when !prerequisitesValidated || !sourcesValidated => "PrerequisiteUnavailable",
            MarketDetailRunStatus.Superseded when !revisionsCurrent => "RevisionChanged",
            _ => null
        };
        run.UpdatedAtUtc = nowUtc;
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            environmentId,
            lease.Key.Environment,
            lease.AppliedEndpointProfile,
            cancellationToken,
            requireExecutable: false).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return resolvedStatus;
    }

    public async Task<bool> SaveValidatedAsync(
        MarketDetailRunLease lease,
        MarketDetailValidatedObservation observation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(observation);
        var environmentId = await ResolveAndValidateLeaseEnvironmentAsync(lease, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await AcquireRunLockAsync(environmentId, lease.Key, cancellationToken).ConfigureAwait(false);
        var run = await LoadOwnedRunAsync(lease, environmentId, cancellationToken).ConfigureAwait(false);
        if (!run.IsUniverseStaged)
        {
            throw new InvalidOperationException("A validated observation cannot be published before the run universe is staged.");
        }

        var target = await dbContext.MarketDetailRunTargets.SingleOrDefaultAsync(
            item => item.RunId == lease.RunId && item.BrokerEnvironmentId == environmentId && item.Epic == observation.Epic,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The observation EPIC is not in this frozen market-detail run.");
        var existing = await dbContext.MarketDetailObservations.SingleOrDefaultAsync(
            item => item.RunId == lease.RunId && item.Epic == observation.Epic,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (target.Status != "Completed")
            {
                throw new InvalidOperationException("A market-detail observation exists for a target not marked complete.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (!await SupersedeIfSourcesChangedAsync(
                lease, environmentId, run, cancellationToken).ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var row = ToEntity(environmentId, lease.RunId, observation);
        ValidateObservationStorageBounds(row, observation);
        dbContext.MarketDetailObservations.Add(row);
        target.Status = "Completed";
        target.Attempts = checked(target.Attempts + 1);
        target.SafeFailureCode = null;
        target.ExclusionEvidenceCode = null;
        target.ExcludedAtUtc = null;
        target.UpdatedAtUtc = observation.RetrievedAtUtc;

        var eligibility = await dbContext.MarketDetailEligibility.SingleOrDefaultAsync(
            item => item.BrokerEnvironmentId == environmentId && item.Epic == observation.Epic,
            cancellationToken).ConfigureAwait(false);
        if (eligibility is null)
        {
            dbContext.MarketDetailEligibility.Add(new MarketDetailEligibilityEntity
            {
                BrokerEnvironmentId = environmentId,
                Epic = observation.Epic,
                Status = "Eligible",
                UpdatedAtUtc = observation.RetrievedAtUtc
            });
        }
        else if (eligibility.Status != "Eligible" && eligibility.ExcludedAtUtc < observation.RetrievedAtUtc)
        {
            eligibility.Status = "Eligible";
            eligibility.EvidenceCode = null;
            eligibility.ExcludedAtUtc = null;
            eligibility.ReinstatedAtUtc = observation.RetrievedAtUtc;
            eligibility.UpdatedAtUtc = observation.RetrievedAtUtc;
        }
        else if (eligibility.Status == "Eligible" && eligibility.UpdatedAtUtc < observation.RetrievedAtUtc)
        {
            eligibility.UpdatedAtUtc = observation.RetrievedAtUtc;
        }

        var current = await dbContext.MarketDetailCurrent.SingleOrDefaultAsync(
            item => item.BrokerEnvironmentId == environmentId && item.Epic == observation.Epic,
            cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            dbContext.MarketDetailCurrent.Add(new MarketDetailCurrentEntity
            {
                BrokerEnvironmentId = environmentId,
                Epic = observation.Epic,
                RunId = lease.RunId,
                RetrievedAtUtc = observation.RetrievedAtUtc,
                UpdatedAtUtc = clock.GetUtcNow().ToUniversalTime()
            });
        }
        else if (observation.RetrievedAtUtc >= current.RetrievedAtUtc)
        {
            current.RunId = lease.RunId;
            current.RetrievedAtUtc = observation.RetrievedAtUtc;
            current.UpdatedAtUtc = clock.GetUtcNow().ToUniversalTime();
        }

        run.UpdatedAtUtc = clock.GetUtcNow().ToUniversalTime();
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            environmentId,
            lease.Key.Environment,
            lease.AppliedEndpointProfile,
            cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> SupersedeIfSourcesChangedAsync(
        MarketDetailRunLease lease,
        Guid environmentId,
        MarketDetailCollectionRunEntity run,
        CancellationToken cancellationToken)
    {
        var cycle = await dbContext.InstrumentCollectionCycleStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId
                && item.TradingDay == lease.Key.TradingDay
                && item.ScheduledSlot == lease.Key.SlotIndex,
                cancellationToken).ConfigureAwait(false);
        if (cycle is not null
            && await EfMarketDetailSourceRevisionGuard.IsCurrentAsync(
                dbContext, environmentId, run, cycle, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        run.Status = "Superseded";
        run.SafeReasonCode = "ListingSourcesChanged";
        ReleaseLease(run, nowUtc);
        run.UpdatedAtUtc = nowUtc;
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            environmentId,
            lease.Key.Environment,
            lease.AppliedEndpointProfile,
            cancellationToken,
            requireExecutable: false).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return false;
    }

    private async Task MarkBlockedAsync(
        MarketDetailRunLease lease,
        string safeReasonCode,
        CancellationToken cancellationToken)
    {
        var environmentId = await ResolveAndValidateLeaseEnvironmentAsync(lease, cancellationToken).ConfigureAwait(false);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await AcquireRunLockAsync(environmentId, lease.Key, cancellationToken).ConfigureAwait(false);
        var run = await LoadOwnedRunAsync(lease, environmentId, cancellationToken).ConfigureAwait(false);
        run.Status = "Blocked";
        run.SafeReasonCode = safeReasonCode;
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        run.UpdatedAtUtc = nowUtc;
        ReleaseLease(run, nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> ValidateUniverseSourcesAsync(
        Guid environmentId,
        MarketDetailRunLease lease,
        MarketDetailUniverse universe,
        CancellationToken cancellationToken)
    {
        var catalogueRevision = await dbContext.MarketCategoryCatalogStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => (long?)item.Revision)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var interestRevision = await dbContext.MarketCategoryInterestStates.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == environmentId)
            .Select(item => (long?)item.Revision)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (catalogueRevision != lease.Revisions.CatalogueRevision || interestRevision != lease.Revisions.InterestRevision)
        {
            return "RevisionChanged";
        }

        var currentInterestedCategories = await (
            from interest in dbContext.MarketCategoryInterests.AsNoTracking()
            join category in dbContext.MarketCategories.AsNoTracking()
                on new { interest.BrokerEnvironmentId, interest.CategoryCode } equals new { category.BrokerEnvironmentId, CategoryCode = category.Code }
            where interest.BrokerEnvironmentId == environmentId
            select interest.CategoryCode).ToListAsync(cancellationToken).ConfigureAwait(false);
        var suppliedCategories = universe.Sources.Select(item => item.CategoryCode).ToHashSet(StringComparer.Ordinal);
        if (!currentInterestedCategories.ToHashSet(StringComparer.Ordinal).SetEquals(suppliedCategories))
        {
            return "ListingSourceSetChanged";
        }

        foreach (var source in universe.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.CategoryCode)
                || source.CategoryCode.Length > 128
                || source.CollectionId == Guid.Empty
                || source.Version < 1
                || !source.IsValidatedComplete)
            {
                return "ListingSourceInvalid";
            }

            var listing = await dbContext.MarketCategoryInstrumentCollectionRuns.AsNoTracking()
                .Where(item => item.CollectionId == source.CollectionId
                    && item.BrokerEnvironmentId == environmentId
                    && item.CategoryCode == source.CategoryCode
                    && item.SnapshotVersion == source.Version
                    && item.CategorySnapshotRevision == lease.Revisions.CatalogueRevision
                    && item.IsComplete)
                .Select(item => new { item.CollectionId, item.SnapshotVersion })
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var catalogState = await dbContext.MarketCategoryInstrumentCatalogStates.AsNoTracking()
                .Where(item => item.BrokerEnvironmentId == environmentId && item.CategoryCode == source.CategoryCode)
                .Select(item => new { item.CollectionId, item.SnapshotVersion })
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (listing is null || catalogState is null
                || catalogState.CollectionId != source.CollectionId
                || catalogState.SnapshotVersion != source.Version)
            {
                return "ListingSourceStale";
            }

            var storedEpics = await dbContext.MarketCategoryInstrumentObservations.AsNoTracking()
                .Where(item => item.CollectionId == source.CollectionId
                    && item.BrokerEnvironmentId == environmentId
                    && item.CategoryCode == source.CategoryCode)
                .Select(item => item.Epic)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            if (!storedEpics.ToHashSet(StringComparer.Ordinal).SetEquals(source.Epics))
            {
                return "ListingContentsChanged";
            }
        }

        return null;
    }

    private async Task<bool> VerifyStagedUniverseMatchesAsync(
        MarketDetailCollectionRunEntity run,
        MarketDetailUniverse universe,
        CancellationToken cancellationToken)
    {
        var persistedSources = await dbContext.MarketDetailRunSources.AsNoTracking()
            .Where(item => item.RunId == run.RunId)
            .OrderBy(item => item.CategoryCode)
            .Select(item => new { item.CategoryCode, item.ListingCollectionId, item.ListingVersion })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var persistedTargets = await dbContext.MarketDetailRunTargets.AsNoTracking()
            .Where(item => item.RunId == run.RunId)
            .OrderBy(item => item.Epic)
            .Select(item => item.Epic)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var expectedSources = universe.Sources.OrderBy(item => item.CategoryCode, StringComparer.Ordinal).ToArray();
        var expectedTargets = universe.Targets.Select(item => item.Epic).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        if (persistedSources.Count != expectedSources.Length
            || persistedTargets.Count != expectedTargets.Length
            || !persistedTargets.SequenceEqual(expectedTargets, StringComparer.Ordinal)
            || persistedSources.Where((item, index) =>
                item.CategoryCode != expectedSources[index].CategoryCode
                || item.ListingCollectionId != expectedSources[index].CollectionId
                || item.ListingVersion != expectedSources[index].Version).Any())
        {
            return false;
        }

        var memberships = await dbContext.MarketDetailRunMemberships.AsNoTracking()
            .Where(item => item.RunId == run.RunId)
            .Select(item => new { item.Epic, item.CategoryCode })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var expectedMemberships = universe.Targets
            .SelectMany(target => target.Memberships.Select(membership => (target.Epic, membership.CategoryCode)))
            .ToHashSet();
        if (!memberships.Select(item => (item.Epic, item.CategoryCode)).ToHashSet().SetEquals(expectedMemberships))
        {
            throw new InvalidOperationException("The market-detail membership set differs from its frozen universe.");
        }

        return true;
    }

    private static void ValidateUniverseShape(
        MarketDetailUniverse universe,
        IReadOnlyCollection<MarketDetailRunMembershipEntity> memberships)
    {
        var targetEpics = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in universe.Targets)
        {
            if (string.IsNullOrWhiteSpace(target.Epic) || target.Epic.Length > 64 || !targetEpics.Add(target.Epic))
            {
                throw new InvalidOperationException("The frozen target set contains an invalid or duplicate EPIC.");
            }
        }

        var categories = universe.Sources.Select(item => item.CategoryCode).ToHashSet(StringComparer.Ordinal);
        if (universe.Sources.Select(item => item.CategoryCode).Distinct(StringComparer.Ordinal).Count() != universe.Sources.Count
            || memberships.Any(item => !targetEpics.Contains(item.Epic) || !categories.Contains(item.CategoryCode)))
        {
            throw new InvalidOperationException("The frozen membership set does not reference its declared sources and targets.");
        }

        var expectedMemberships = universe.Sources
            .SelectMany(source => source.Epics.Distinct(StringComparer.Ordinal).Select(epic => (Epic: epic, source.CategoryCode)))
            .ToHashSet();
        var actualMemberships = memberships.Select(item => (Epic: item.Epic, item.CategoryCode)).ToHashSet();
        if (!expectedMemberships.SetEquals(actualMemberships)
            || !expectedMemberships.Select(item => item.Epic).ToHashSet(StringComparer.Ordinal).SetEquals(targetEpics))
        {
            throw new InvalidOperationException("The frozen targets or memberships do not exactly cover the validated listing sources.");
        }
    }

    private async Task MarkEligibilityExcludedAsync(
        Guid environmentId,
        string epic,
        string evidenceCode,
        DateTimeOffset excludedAtUtc,
        CancellationToken cancellationToken)
    {
        var eligibility = await dbContext.MarketDetailEligibility.SingleOrDefaultAsync(
            item => item.BrokerEnvironmentId == environmentId && item.Epic == epic,
            cancellationToken).ConfigureAwait(false);
        if (eligibility is null)
        {
            dbContext.MarketDetailEligibility.Add(new MarketDetailEligibilityEntity
            {
                BrokerEnvironmentId = environmentId,
                Epic = epic,
                Status = "Excluded",
                EvidenceCode = evidenceCode,
                ExcludedAtUtc = excludedAtUtc,
                UpdatedAtUtc = excludedAtUtc
            });
            return;
        }

        eligibility.Status = "Excluded";
        eligibility.EvidenceCode = evidenceCode;
        eligibility.ExcludedAtUtc = excludedAtUtc;
        eligibility.ReinstatedAtUtc = null;
        eligibility.UpdatedAtUtc = excludedAtUtc;
    }

    private async Task<Guid> ResolveAndValidateLeaseEnvironmentAsync(
        MarketDetailRunLease lease,
        CancellationToken cancellationToken)
    {
        if (lease.RunId == Guid.Empty || lease.Owner == Guid.Empty || lease.Fence < 1
            || lease.ExpiresAtUtc.Offset != TimeSpan.Zero || lease.WindowEndUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The market-detail lease is invalid.", nameof(lease));
        }

        return await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext,
            contextResolver,
            lease.Key.Environment,
            cancellationToken,
            requireExecutable: false).ConfigureAwait(false);
    }

    private async Task<MarketDetailCollectionRunEntity> LoadOwnedRunAsync(
        MarketDetailRunLease lease,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.MarketDetailCollectionRuns.SingleOrDefaultAsync(
            item => item.RunId == lease.RunId && item.BrokerEnvironmentId == environmentId,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The market-detail run no longer exists in the applied environment.");
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        if (run.LeaseOwner != lease.Owner || run.LeaseFence != lease.Fence
            || run.LeaseExpiresAtUtc is not { } leaseExpiresAtUtc || leaseExpiresAtUtc <= nowUtc || run.WindowEndUtc <= nowUtc
            || run.EndpointProfile != lease.AppliedEndpointProfile
            || run.CatalogueRevision != lease.Revisions.CatalogueRevision
            || run.InterestRevision != lease.Revisions.InterestRevision
            || run.ScheduleRevision != lease.Revisions.ScheduleRevision
            || run.Status != "Collecting")
        {
            throw new InvalidOperationException("The market-detail run lease or frozen provenance is stale.");
        }

        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            environmentId,
            lease.Key.Environment,
            lease.AppliedEndpointProfile,
            cancellationToken).ConfigureAwait(false);
        return run;
    }

    private async Task ValidateOwnedRunForReadAsync(
        MarketDetailRunLease lease,
        Guid environmentId,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.MarketDetailCollectionRuns.AsNoTracking().SingleOrDefaultAsync(
            item => item.RunId == lease.RunId && item.BrokerEnvironmentId == environmentId,
            cancellationToken).ConfigureAwait(false);
        var nowUtc = clock.GetUtcNow().ToUniversalTime();
        if (run is null || run.LeaseOwner != lease.Owner || run.LeaseFence != lease.Fence
            || run.LeaseExpiresAtUtc is not { } leaseExpiresAtUtc || leaseExpiresAtUtc <= nowUtc || run.WindowEndUtc <= nowUtc
            || run.Status != "Collecting" || run.EndpointProfile != lease.AppliedEndpointProfile
            || run.CatalogueRevision != lease.Revisions.CatalogueRevision
            || run.InterestRevision != lease.Revisions.InterestRevision
            || run.ScheduleRevision != lease.Revisions.ScheduleRevision)
        {
            throw new InvalidOperationException("The market-detail run lease is stale or no longer active.");
        }
    }

    private async Task InsertBatchesAsync<TEntity>(
        IReadOnlyList<TEntity> entities,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        for (var offset = 0; offset < entities.Count; offset += MaximumBatchSize)
        {
            var batch = entities.Skip(offset).Take(MaximumBatchSize).ToArray();
            dbContext.AddRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var entity in batch)
            {
                dbContext.Entry(entity).State = EntityState.Detached;
            }
        }
    }

    private static MarketDetailRunLease ToLease(
        MarketDetailCollectionRunEntity run,
        BrokerEnvironmentKind environment) =>
        new(
            run.RunId,
            new(environment, run.TradingDay, run.ScheduledSlot),
            run.LeaseOwner!.Value,
            run.LeaseFence,
            run.LeaseExpiresAtUtc!.Value,
            run.WindowEndUtc,
            run.EndpointProfile,
            new(run.CatalogueRevision, run.InterestRevision, run.ScheduleRevision));

    private static string ToStoredStatus(MarketDetailRunStatus status) => status switch
    {
        MarketDetailRunStatus.Running => "Collecting",
        MarketDetailRunStatus.Incomplete => "Incomplete",
        MarketDetailRunStatus.Complete => "Complete",
        MarketDetailRunStatus.Blocked => "Blocked",
        MarketDetailRunStatus.Superseded => "Superseded",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static void ValidateAcquireArguments(
        MarketDetailRunKey key,
        MarketDetailRevisions revisions,
        string appliedEndpointProfile,
        Guid owner,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        DateTimeOffset windowEndUtc)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(revisions);
        if (!Enum.IsDefined(key.Environment)
            || key.SlotIndex is < 0 or > 3
            || revisions.CatalogueRevision < 0
            || revisions.InterestRevision < 0
            || revisions.ScheduleRevision < 0
            || string.IsNullOrWhiteSpace(appliedEndpointProfile)
            || appliedEndpointProfile.Length > 128
            || owner == Guid.Empty
            || nowUtc.Offset != TimeSpan.Zero
            || windowEndUtc.Offset != TimeSpan.Zero
            || leaseDuration <= TimeSpan.Zero
            || windowEndUtc <= nowUtc)
        {
            throw new ArgumentException("The market-detail run key, revisions, profile, or lease window is invalid.");
        }
    }

    private async Task AcquireRunLockAsync(
        Guid environmentId,
        MarketDetailRunKey key,
        CancellationToken cancellationToken) =>
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext,
            $"MarketDetails/{environmentId:N}/{key.TradingDay:yyyyMMdd}/{key.SlotIndex}",
            "Exclusive",
            cancellationToken).ConfigureAwait(false);

    private static void ReleaseLease(MarketDetailCollectionRunEntity run, DateTimeOffset nowUtc)
    {
        run.LeaseOwner = null;
        run.LeaseExpiresAtUtc = null;
        run.UpdatedAtUtc = nowUtc;
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;

    private static MarketDetailObservationEntity ToEntity(
        Guid environmentId,
        Guid runId,
        MarketDetailValidatedObservation observation)
    {
        var instrument = observation.Instrument;
        var rules = observation.DealingRules;
        var snapshot = observation.Snapshot;
        return new()
        {
            RunId = runId,
            BrokerEnvironmentId = environmentId,
            Epic = observation.Epic,
            RetrievedAtUtc = observation.RetrievedAtUtc,
            SourceEndpoint = observation.SourceEndpoint,
            SourceVersion = observation.SourceVersion,
            DetailSchemaVersion = DetailSchemaVersion,
            ProviderUpdateTimeText = observation.ProviderUpdateTimeText,
            InstrumentName = instrument.Name,
            InstrumentType = instrument.Type,
            MarketId = instrument.MarketId,
            Expiry = instrument.Expiry,
            InstrumentUnit = instrument.Unit,
            LotSize = instrument.LotSize,
            ForceOpenAllowed = instrument.ForceOpenAllowed,
            StopsLimitsAllowed = instrument.StopsLimitsAllowed,
            ControlledRiskAllowed = instrument.ControlledRiskAllowed,
            StreamingPricesAvailable = instrument.StreamingPricesAvailable,
            MarginFactor = instrument.MarginFactor,
            MarginFactorUnit = instrument.MarginFactorUnit,
            InstrumentJson = JsonSerializer.Serialize(instrument, JsonOptions),
            DealingRulesJson = JsonSerializer.Serialize(rules, JsonOptions),
            SnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions),
            MarketStatus = snapshot.MarketStatus,
            DelayTime = snapshot.DelayTime.Value,
            Bid = snapshot.Bid.Value,
            Offer = snapshot.Offer.Value,
            High = snapshot.High.Value,
            Low = snapshot.Low.Value,
            NetChange = snapshot.NetChange.Value,
            PercentageChange = snapshot.PercentageChange.Value,
            BinaryOdds = snapshot.BinaryOdds.Value,
            DecimalPlacesFactor = snapshot.DecimalPlacesFactor.Value,
            ScalingFactor = snapshot.ScalingFactor.Value,
            ControlledRiskExtraSpread = snapshot.ControlledRiskExtraSpread.Value,
            MinStepDistance = rules.MinStepDistance.Value,
            MinStepDistanceUnit = rules.MinStepDistance.Unit,
            MinDealSize = rules.MinDealSize.Value,
            MinDealSizeUnit = rules.MinDealSize.Unit,
            MinControlledRiskStopDistance = rules.MinControlledRiskStopDistance.Value,
            MinControlledRiskStopDistanceUnit = rules.MinControlledRiskStopDistance.Unit,
            MinNormalStopOrLimitDistance = rules.MinNormalStopOrLimitDistance.Value,
            MinNormalStopOrLimitDistanceUnit = rules.MinNormalStopOrLimitDistance.Unit,
            MaxStopOrLimitDistance = rules.MaxStopOrLimitDistance.Value,
            MaxStopOrLimitDistanceUnit = rules.MaxStopOrLimitDistance.Unit,
            ControlledRiskSpacing = rules.ControlledRiskSpacing.Value,
            ControlledRiskSpacingUnit = rules.ControlledRiskSpacing.Unit,
            MarketOrderPreference = rules.MarketOrderPreference,
            TrailingStopsPreference = rules.TrailingStopsPreference
        };
    }

    private static void ValidateObservationStorageBounds(
        MarketDetailObservationEntity entity,
        MarketDetailValidatedObservation value)
    {
        if (entity.Epic.Length > 64
            || entity.SourceEndpoint.Length > 256
            || entity.InstrumentName.Length > 256
            || entity.InstrumentType?.Length > 64
            || entity.MarketId?.Length > 64
            || entity.Expiry?.Length > 32
            || entity.InstrumentUnit?.Length > 32
            || entity.MarginFactorUnit?.Length > 32
            || entity.MarketStatus.Length > 32
            || entity.ProviderUpdateTimeText?.Length > 32
            || entity.InstrumentJson.Length > 32_768
            || entity.DealingRulesJson.Length > 8_192
            || entity.SnapshotJson.Length > 8_192
            || !HasSqlDecimal28_10(entity.LotSize)
            || !HasSqlDecimal28_10(entity.MarginFactor)
            || !HasSqlDecimal28_10(entity.DelayTime)
            || !HasSqlDecimal28_10(entity.Bid)
            || !HasSqlDecimal28_10(entity.Offer)
            || !HasSqlDecimal28_10(entity.High)
            || !HasSqlDecimal28_10(entity.Low)
            || !HasSqlDecimal28_10(entity.NetChange)
            || !HasSqlDecimal28_10(entity.PercentageChange)
            || !HasSqlDecimal28_10(entity.BinaryOdds)
            || !HasSqlDecimal28_10(entity.DecimalPlacesFactor)
            || !HasSqlDecimal28_10(entity.ScalingFactor)
            || !HasSqlDecimal28_10(entity.ControlledRiskExtraSpread)
            || !HasSqlDecimal28_10(entity.MinStepDistance)
            || !HasSqlDecimal28_10(entity.MinDealSize)
            || !HasSqlDecimal28_10(entity.MinControlledRiskStopDistance)
            || !HasSqlDecimal28_10(entity.MinNormalStopOrLimitDistance)
            || !HasSqlDecimal28_10(entity.MaxStopOrLimitDistance)
            || !HasSqlDecimal28_10(entity.ControlledRiskSpacing)
            || !HasSqlDecimal28_10(value.Instrument.Currencies.SelectMany(item => new[] { item.BaseExchangeRate, item.ExchangeRate }))
            || !HasSqlDecimal28_10(value.Instrument.MarginDepositBands.SelectMany(item => new[] { item.Minimum, item.Margin, item.Maximum.Value }))
            || !HasSqlDecimal28_10(value.Instrument.SlippageFactor.Value)
            || !HasSqlDecimal28_10(value.Instrument.LimitedRiskPremium.Value)
            || !HasSqlDecimal28_10(value.Instrument.SprintMarketsMinimumExpiryTime.Value)
            || !HasSqlDecimal28_10(value.Instrument.SprintMarketsMaximumExpiryTime.Value)
            || !HasSqlDecimal28_10(value.DealingRules.ControlledRiskSpacing.Value)
            || !HasSqlDecimal28_10(value.DealingRules.MaxStopOrLimitDistance.Value)
            || !HasSqlDecimal28_10(value.DealingRules.MinControlledRiskStopDistance.Value)
            || !HasSqlDecimal28_10(value.DealingRules.MinDealSize.Value)
            || !HasSqlDecimal28_10(value.DealingRules.MinNormalStopOrLimitDistance.Value)
            || !HasSqlDecimal28_10(value.DealingRules.MinStepDistance.Value)
            || !HasSqlDecimal28_10(value.Snapshot.NetChange.Value)
            || !HasSqlDecimal28_10(value.Snapshot.PercentageChange.Value)
            || !HasSqlDecimal28_10(value.Snapshot.DelayTime.Value)
            || !HasSqlDecimal28_10(value.Snapshot.Bid.Value)
            || !HasSqlDecimal28_10(value.Snapshot.Offer.Value)
            || !HasSqlDecimal28_10(value.Snapshot.High.Value)
            || !HasSqlDecimal28_10(value.Snapshot.Low.Value)
            || !HasSqlDecimal28_10(value.Snapshot.BinaryOdds.Value)
            || !HasSqlDecimal28_10(value.Snapshot.DecimalPlacesFactor.Value)
            || !HasSqlDecimal28_10(value.Snapshot.ScalingFactor.Value)
            || !HasSqlDecimal28_10(value.Snapshot.ControlledRiskExtraSpread.Value))
        {
            throw new InvalidOperationException("The validated market-detail observation exceeds its bounded SQL storage contract or SQL decimal precision.");
        }
    }

    private static bool HasSqlDecimal28_10(IEnumerable<decimal?> values) => values.All(value => HasSqlDecimal28_10(value));

    private static bool HasSqlDecimal28_10(IEnumerable<decimal> values) => values.All(value => HasSqlDecimal28_10(value));

    private static bool HasSqlDecimal28_10(decimal? value) =>
        value is null || HasSqlDecimal28_10(value.Value);

    private static bool HasSqlDecimal28_10(decimal value)
    {
        const decimal maximum = 999999999999999999.9999999999m;
        return value is >= -maximum and <= maximum && decimal.Round(value, 10) == value;
    }

}
