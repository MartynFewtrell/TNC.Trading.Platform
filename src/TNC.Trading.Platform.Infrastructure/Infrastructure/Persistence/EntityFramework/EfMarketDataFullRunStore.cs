using System.Data;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;

internal sealed class EfMarketDataFullRunStore(
    PlatformDbContext dbContext,
    IAppliedBrokerEnvironmentContextResolver? contextResolver = null) : IMarketDataFullRunStore
{
    private const int MaximumAttempts = 3;
    private static readonly HashSet<string> StageStatuses =
        ["Succeeded", "Failed", "Stale", "Skipped"];
    private static readonly HashSet<string> CompletionOutcomes =
        ["Succeeded", "Partial", "Failed", "Idle", "Cancelled"];

    public async Task<MarketDataFullRunAdmissionResult> TryAdmitAsync(
        MarketDataFullRunAdmissionRequest request,
        CancellationToken cancellationToken)
    {
        ValidateAdmissionRequest(request);
        var appliedId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext,
            contextResolver,
            request.Environment,
            cancellationToken).ConfigureAwait(false);
        if (appliedId != request.AppliedBrokerEnvironmentId)
        {
            throw new InvalidOperationException("The applied broker environment changed before full-run admission.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext,
            $"MarketDataFullRuns/{appliedId:N}",
            "Exclusive",
            cancellationToken).ConfigureAwait(false);

        var existing = await dbContext.MarketDataFullRuns
            .SingleOrDefaultAsync(
                item => item.BrokerEnvironmentId == appliedId && item.Status == "Running",
                cancellationToken).ConfigureAwait(false);
        if (existing is not null
            && !string.Equals(existing.EndpointProfile, request.EndpointProfile, StringComparison.Ordinal))
        {
            existing.Status = "Superseded";
            existing.Outcome = "Superseded";
            existing.SafeReasonCode = "AppliedProfileChanged";
            existing.LeaseOwner = null;
            existing.LeaseExpiresAtUtc = null;
            existing.CompletedAtUtc = request.AdmittedAtUtc;
            existing.UpdatedAtUtc = request.AdmittedAtUtc;
            await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
                dbContext,
                contextResolver,
                appliedId,
                request.Environment,
                request.EndpointProfile,
                cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            existing = null;
        }

        if (existing is not null)
        {
            if (request.Trigger is MarketDataFullRunTrigger.Interest or MarketDataFullRunTrigger.Configuration)
            {
                await UpsertIntentAsync(
                    appliedId,
                    request.Trigger,
                    request.CollectionConfigurationVersion,
                    request.InterestRevision,
                    request.AdmittedAtUtc,
                    cancellationToken).ConfigureAwait(false);
            }

            await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
                dbContext,
                contextResolver,
                appliedId,
                request.Environment,
                request.EndpointProfile,
                cancellationToken).ConfigureAwait(false);
            await AddActiveRunCoverageAsync(existing, request, cancellationToken).ConfigureAwait(false);
            if (existing.LeaseExpiresAtUtc > request.AdmittedAtUtc
                && existing.LeaseOwner is not null)
            {
                var activeLease = await CreateLeaseAsync(
                    existing,
                    request.Environment,
                    existing.LeaseOwner.Value,
                    existing.LeaseFence,
                    cancellationToken).ConfigureAwait(false);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(MarketDataFullRunAdmissionStatus.AlreadyRunning, activeLease);
            }

            existing.LeaseOwner = request.LeaseOwner;
            existing.LeaseFence = checked(existing.LeaseFence + 1);
            existing.LeaseExpiresAtUtc = request.AdmittedAtUtc + request.LeaseDuration;
            existing.UpdatedAtUtc = request.AdmittedAtUtc;
            await AddActiveRunCoverageAsync(existing, request, cancellationToken).ConfigureAwait(false);
            await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
                dbContext,
                contextResolver,
                appliedId,
                request.Environment,
                request.EndpointProfile,
                cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            var resumedLease = await CreateLeaseAsync(
                existing,
                request.Environment,
                request.LeaseOwner,
                existing.LeaseFence,
                cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(MarketDataFullRunAdmissionStatus.Resumed, resumedLease);
        }

        if (request.ResumeOnly)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(MarketDataFullRunAdmissionStatus.OutsideWindow, null);
        }

        if (request.AdmittedAtUtc >= request.WindowEndUtc)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(MarketDataFullRunAdmissionStatus.OutsideWindow, null);
        }

        var pendingIntent = await dbContext.MarketDataFullRunIntents
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == appliedId, cancellationToken)
            .ConfigureAwait(false);
        var trigger = request.Trigger;
        if (pendingIntent is not null && request.Trigger is not MarketDataFullRunTrigger.FailedItemRetry)
        {
            if (!Enum.TryParse<MarketDataFullRunTrigger>(pendingIntent.Trigger, out var pendingTrigger))
            {
                throw new InvalidOperationException("The persisted full-run intent has an unsupported trigger.");
            }

            if (pendingIntent.CollectionConfigurationVersion <= request.CollectionConfigurationVersion
                && pendingIntent.InterestRevision <= request.InterestRevision
                && pendingIntent.UpdatedAtUtc <= request.AdmittedAtUtc)
            {
                trigger = pendingTrigger;
                dbContext.MarketDataFullRunIntents.Remove(pendingIntent);
            }
        }

        MarketDataFullRunEntity? retrySource = null;
        if (request.Trigger is MarketDataFullRunTrigger.FailedItemRetry)
        {
            if (request.RetrySourceRunId is not { } retrySourceRunId)
            {
                throw new ArgumentException("A failed-item retry must identify its source full run.", nameof(request));
            }

            retrySource = await dbContext.MarketDataFullRuns.SingleOrDefaultAsync(
                item => item.RunId == retrySourceRunId && item.BrokerEnvironmentId == appliedId,
                cancellationToken).ConfigureAwait(false);
            if (retrySource is null
                || retrySource.FailedItemFollowUpDueAtUtc is not { } dueAtUtc
                || dueAtUtc > request.AdmittedAtUtc
                || retrySource.FailedItemFollowUpRunId is not null
                || retrySource.FailedItemFollowUpCancelledAtUtc is not null
                || retrySource.WindowEndUtc <= request.AdmittedAtUtc
                || retrySource.Status != "Completed"
                || retrySource.TradingDay != request.TradingDay
                || retrySource.WindowEndUtc != request.WindowEndUtc
                || retrySource.ScheduleRevision != request.ScheduleRevision
                || retrySource.EffectiveUpdatesPerDay != request.EffectiveUpdatesPerDay
                || retrySource.CollectionConfigurationVersion != request.CollectionConfigurationVersion
                || retrySource.InterestRevision != request.InterestRevision
                || retrySource.DetailScheduledSlot != request.DetailScheduledSlot
                || request.CoveredSlots.Count != 0
                || !string.Equals(retrySource.EndpointProfile, request.EndpointProfile, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(MarketDataFullRunAdmissionStatus.OutsideWindow, null);
            }

            var sourceCategoryCodes = await dbContext.MarketDataFullRunCategories.AsNoTracking()
                .Where(item => item.RunId == retrySource.RunId)
                .Select(item => item.CategoryCode)
                .ToHashSetAsync(cancellationToken).ConfigureAwait(false);
            if (!sourceCategoryCodes.SetEquals(request.SelectedCategoryCodes))
            {
                throw new ArgumentException("A failed-item retry must preserve its source run's frozen categories.", nameof(request));
            }
        }
        else if (request.RetrySourceRunId is not null)
        {
            throw new ArgumentException("Only a failed-item retry can identify a source full run.", nameof(request));
        }

        var existingCoverage = await dbContext.MarketDataFullRunSlotCoverages
            .Where(item => item.BrokerEnvironmentId == appliedId
                && item.TradingDay == request.TradingDay
                && item.ScheduleRevision == request.ScheduleRevision)
            .Join(
                dbContext.MarketDataFullRuns,
                coverage => new { coverage.RunId, coverage.BrokerEnvironmentId },
                run => new { run.RunId, run.BrokerEnvironmentId },
                (coverage, run) => new { Coverage = coverage, run.EndpointProfile })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var isScheduledTrigger = trigger is MarketDataFullRunTrigger.Scheduled or MarketDataFullRunTrigger.CatchUp;
        var coverageSlots = isScheduledTrigger
            ? existingCoverage
                .Where(item => string.Equals(item.EndpointProfile, request.EndpointProfile, StringComparison.Ordinal))
                .Select(item => item.Coverage.ScheduledSlot)
                .ToHashSet()
            : existingCoverage.Select(item => item.Coverage.ScheduledSlot).ToHashSet();
        var newCoveredSlots = request.CoveredSlots
            .Where(slot => !coverageSlots.Contains(slot.ScheduledSlot))
            .ToArray();
        if (isScheduledTrigger
            && request.CoveredSlots.Count > 0
            && request.CoveredSlots.All(slot => coverageSlots.Contains(slot.ScheduledSlot)))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(MarketDataFullRunAdmissionStatus.AlreadyCovered, null);
        }

        var entity = new MarketDataFullRunEntity
        {
            RunId = Guid.NewGuid(),
            BrokerEnvironmentId = appliedId,
            EndpointProfile = request.EndpointProfile,
            TradingDay = request.TradingDay,
            DetailScheduledSlot = request.DetailScheduledSlot
                ?? request.CoveredSlots.FirstOrDefault()?.ScheduledSlot
                ?? 0,
            AdmittedAtUtc = request.AdmittedAtUtc,
            WindowEndUtc = request.WindowEndUtc,
            ScheduleRevision = request.ScheduleRevision,
            EffectiveUpdatesPerDay = request.EffectiveUpdatesPerDay,
            CollectionConfigurationVersion = request.CollectionConfigurationVersion,
            InterestRevision = request.InterestRevision,
            Trigger = trigger.ToString(),
            Status = "Running",
            CurrentStage = trigger is MarketDataFullRunTrigger.FailedItemRetry
                ? MarketDataFullRunStage.Details.ToString()
                : MarketDataFullRunStage.Categories.ToString(),
            LeaseOwner = request.LeaseOwner,
            LeaseFence = 1,
            LeaseExpiresAtUtc = request.AdmittedAtUtc + request.LeaseDuration,
            CreatedAtUtc = request.AdmittedAtUtc,
            UpdatedAtUtc = request.AdmittedAtUtc
        };
        dbContext.MarketDataFullRuns.Add(entity);
        dbContext.MarketDataFullRunCategories.AddRange(request.SelectedCategoryCodes
            .Select(code => new MarketDataFullRunCategoryEntity
            {
                RunId = entity.RunId,
                CategoryCode = code
            }));
        dbContext.MarketDataFullRunStages.AddRange(Enum.GetValues<MarketDataFullRunStage>()
            .Select(stage => new MarketDataFullRunStageEntity
            {
                RunId = entity.RunId,
                Stage = stage.ToString(),
                Status = trigger is MarketDataFullRunTrigger.FailedItemRetry
                    && stage is MarketDataFullRunStage.Categories or MarketDataFullRunStage.Listings
                        ? "Skipped"
                        : "Pending",
                SafeReasonCode = trigger is MarketDataFullRunTrigger.FailedItemRetry
                    && stage is MarketDataFullRunStage.Categories or MarketDataFullRunStage.Listings
                        ? "FailedItemOnlyRetry"
                        : null,
                UpdatedAtUtc = request.AdmittedAtUtc
            }));

        var existingCoverageSlots = existingCoverage
            .Select(item => item.Coverage.ScheduledSlot)
            .ToHashSet();
        dbContext.MarketDataFullRunSlotCoverages.AddRange(newCoveredSlots
            .Where(slot => !existingCoverageSlots.Contains(slot.ScheduledSlot))
            .Select(slot => new MarketDataFullRunSlotCoverageEntity
            {
                BrokerEnvironmentId = appliedId,
                TradingDay = slot.TradingDay,
                ScheduleRevision = slot.ScheduleRevision,
                ScheduledSlot = slot.ScheduledSlot,
                RunId = entity.RunId,
                CoverageKind = GetCoverageKind(trigger),
                CoveredAtUtc = request.AdmittedAtUtc
            }));
        if (isScheduledTrigger)
        {
            var scheduledSlots = newCoveredSlots.Select(slot => slot.ScheduledSlot).ToHashSet();
            foreach (var staleCoverage in existingCoverage
                .Where(item => scheduledSlots.Contains(item.Coverage.ScheduledSlot)
                    && !string.Equals(item.EndpointProfile, request.EndpointProfile, StringComparison.Ordinal)))
            {
                staleCoverage.Coverage.RunId = entity.RunId;
                staleCoverage.Coverage.CoverageKind = GetCoverageKind(trigger);
                staleCoverage.Coverage.CoveredAtUtc = request.AdmittedAtUtc;
            }
        }

        if (retrySource is not null)
        {
            retrySource.FailedItemFollowUpRunId = entity.RunId;
            retrySource.UpdatedAtUtc = request.AdmittedAtUtc;
        }

        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            appliedId,
            request.Environment,
            request.EndpointProfile,
            cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new(
            MarketDataFullRunAdmissionStatus.Admitted,
            new MarketDataFullRunLease(
                entity.RunId,
                request.Environment,
                appliedId,
                entity.EndpointProfile,
                entity.TradingDay,
                entity.AdmittedAtUtc,
                entity.WindowEndUtc,
                entity.ScheduleRevision,
                entity.EffectiveUpdatesPerDay,
                entity.CollectionConfigurationVersion,
                entity.InterestRevision,
                trigger,
                request.SelectedCategoryCodes,
                request.LeaseOwner,
                entity.LeaseFence,
                entity.LeaseExpiresAtUtc!.Value,
                newCoveredSlots));
    }

    public async Task<bool> TryRenewLeaseAsync(
        MarketDataFullRunLease lease,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ValidateLease(lease, nowUtc, leaseDuration);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext,
            $"MarketDataFullRuns/{lease.AppliedBrokerEnvironmentId:N}",
            "Exclusive",
            cancellationToken).ConfigureAwait(false);
        var entity = await FindOwnedRunAsync(lease, cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.LeaseExpiresAtUtc <= nowUtc)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            lease.AppliedBrokerEnvironmentId,
            lease.Environment,
            lease.EndpointProfile,
            cancellationToken).ConfigureAwait(false);
        entity.LeaseExpiresAtUtc = nowUtc + leaseDuration;
        entity.UpdatedAtUtc = nowUtc;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RecordSlotCoverageAsync(
        MarketDataFullRunLease lease,
        MarketDataFullRunSlotIdentity slot,
        DateTimeOffset coveredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(slot);
        if (slot.TradingDay != lease.TradingDay
            || slot.ScheduleRevision != lease.ScheduleRevision
            || slot.ScheduledSlot < 0
            || coveredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A valid same-day slot identity and UTC coverage instant are required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext,
            $"MarketDataFullRuns/{lease.AppliedBrokerEnvironmentId:N}",
            "Exclusive",
            cancellationToken).ConfigureAwait(false);
        var run = await FindOwnedRunAsync(lease, cancellationToken).ConfigureAwait(false);
        if (run is null || run.LeaseExpiresAtUtc <= coveredAtUtc)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var existing = await dbContext.MarketDataFullRunSlotCoverages
            .SingleOrDefaultAsync(
                item => item.BrokerEnvironmentId == lease.AppliedBrokerEnvironmentId
                    && item.TradingDay == slot.TradingDay
                    && item.ScheduleRevision == slot.ScheduleRevision
                    && item.ScheduledSlot == slot.ScheduledSlot,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return existing.RunId == lease.RunId;
        }

        dbContext.MarketDataFullRunSlotCoverages.Add(new MarketDataFullRunSlotCoverageEntity
        {
            BrokerEnvironmentId = lease.AppliedBrokerEnvironmentId,
            TradingDay = slot.TradingDay,
            ScheduleRevision = slot.ScheduleRevision,
            ScheduledSlot = slot.ScheduledSlot,
            RunId = lease.RunId,
            CoverageKind = "ActiveRun",
            CoveredAtUtc = coveredAtUtc
        });
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            lease.AppliedBrokerEnvironmentId,
            lease.Environment,
            lease.EndpointProfile,
            cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<bool> RecordStageAttemptAsync(
        MarketDataFullRunLease lease,
        MarketDataFullRunStage stage,
        string status,
        DateTimeOffset nowUtc,
        bool succeeded,
        string? safeReasonCode,
        CancellationToken cancellationToken) =>
        RecordAttemptAsync(
            lease,
            stage,
            null,
            status,
            nowUtc,
            succeeded,
            safeReasonCode,
            cancellationToken);

    public Task<bool> RecordItemAttemptAsync(
        MarketDataFullRunLease lease,
        MarketDataFullRunStage stage,
        string itemCode,
        string status,
        DateTimeOffset nowUtc,
        bool succeeded,
        string? safeReasonCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(itemCode) || itemCode.Length > 128)
        {
            throw new ArgumentException("A valid full-run item code is required.", nameof(itemCode));
        }

        return RecordAttemptAsync(
            lease,
            stage,
            itemCode,
            status,
            nowUtc,
            succeeded,
            safeReasonCode,
            cancellationToken);
    }

    public async Task<string?> GetStageStatusAsync(
        MarketDataFullRunLease lease,
        MarketDataFullRunStage stage,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        if (!await EfMarketDataFullRunLeaseGuard.IsActiveInTransactionAsync(
                dbContext,
                lease,
                nowUtc,
                contextResolver,
                cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var status = await dbContext.MarketDataFullRunStages.AsNoTracking()
            .Where(item => item.RunId == lease.RunId && item.Stage == stage.ToString())
            .Select(item => item.Status)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return status;
    }

    public async Task<IReadOnlySet<string>> GetSucceededItemsAsync(
        MarketDataFullRunLease lease,
        MarketDataFullRunStage stage,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        if (!await EfMarketDataFullRunLeaseGuard.IsActiveInTransactionAsync(
                dbContext,
                lease,
                nowUtc,
                contextResolver,
                cancellationToken).ConfigureAwait(false))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var succeededItems = await dbContext.MarketDataFullRunItems.AsNoTracking()
            .Where(item => item.RunId == lease.RunId
                && item.Stage == stage.ToString()
                && item.Status == "Succeeded")
            .Select(item => item.ItemCode)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return succeededItems.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<bool> CompleteAsync(
        MarketDataFullRunLease lease,
        string outcome,
        string? safeReasonCode,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (!CompletionOutcomes.Contains(outcome)
            || completedAtUtc.Offset != TimeSpan.Zero
            || !IsSafeReasonCode(safeReasonCode))
        {
            throw new ArgumentException("A valid full-run outcome, completion instant, and safe reason code are required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext,
            $"MarketDataFullRuns/{lease.AppliedBrokerEnvironmentId:N}",
            "Exclusive",
            cancellationToken).ConfigureAwait(false);
        var entity = await FindOwnedRunAsync(lease, cancellationToken).ConfigureAwait(false);
        if (entity is null || entity.LeaseExpiresAtUtc <= completedAtUtc)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            lease.AppliedBrokerEnvironmentId,
            lease.Environment,
            lease.EndpointProfile,
            cancellationToken).ConfigureAwait(false);
        if (entity.Trigger != MarketDataFullRunTrigger.FailedItemRetry.ToString()
            && entity.FailedItemFollowUpDueAtUtc is null)
        {
            var scheduledSlot = lease.DetailScheduledSlot
                ?? lease.CoveredSlots?.FirstOrDefault()?.ScheduledSlot
                ?? 0;
            var detailRunId = await dbContext.MarketDetailCollectionRuns.AsNoTracking()
                .Where(item => item.BrokerEnvironmentId == lease.AppliedBrokerEnvironmentId
                    && item.TradingDay == lease.TradingDay
                    && item.ScheduledSlot == scheduledSlot
                    && item.ScheduleRevision == lease.ScheduleRevision)
                .Select(item => (Guid?)item.RunId)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (detailRunId is { } runId)
            {
                var lastThirdFailureAtUtc = await dbContext.MarketDetailRunTargets.AsNoTracking()
                    .Where(item => item.RunId == runId
                        && item.BrokerEnvironmentId == lease.AppliedBrokerEnvironmentId
                        && item.Status == "Failed"
                        && item.CanRetry
                        && item.Attempts == MaximumAttempts)
                    .Select(item => (DateTimeOffset?)item.UpdatedAtUtc)
                    .MaxAsync(cancellationToken).ConfigureAwait(false);
                if (lastThirdFailureAtUtc is { } failedAtUtc)
                {
                    entity.FailedItemFollowUpDueAtUtc = failedAtUtc.AddMinutes(15);
                }
            }
        }

        entity.Status = "Completed";
        entity.Outcome = outcome;
        entity.SafeReasonCode = safeReasonCode;
        entity.CompletedAtUtc = completedAtUtc;
        entity.UpdatedAtUtc = completedAtUtc;
        entity.LeaseOwner = null;
        entity.LeaseExpiresAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task RecordIntentAsync(
        BrokerEnvironmentKind environment,
        Guid appliedBrokerEnvironmentId,
        MarketDataFullRunTrigger trigger,
        long collectionConfigurationVersion,
        long interestRevision,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        if (trigger is not (MarketDataFullRunTrigger.Interest or MarketDataFullRunTrigger.Configuration)
            || appliedBrokerEnvironmentId == Guid.Empty
            || collectionConfigurationVersion < 1
            || interestRevision < 0
            || updatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A valid applied-environment reassessment intent is required.");
        }

        var resolvedId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext,
            contextResolver,
            environment,
            cancellationToken,
            requireExecutable: false).ConfigureAwait(false);
        if (resolvedId != appliedBrokerEnvironmentId)
        {
            throw new InvalidOperationException("The applied broker environment changed before the full-run intent was recorded.");
        }

        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken).ConfigureAwait(false)
            : null;
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext,
            $"MarketDataFullRuns/{appliedBrokerEnvironmentId:N}",
            "Exclusive",
            cancellationToken).ConfigureAwait(false);
        await UpsertIntentAsync(
            appliedBrokerEnvironmentId,
            trigger,
            collectionConfigurationVersion,
            interestRevision,
            updatedAtUtc,
            cancellationToken).ConfigureAwait(false);
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            appliedBrokerEnvironmentId,
            environment,
            null,
            cancellationToken,
            requireExecutable: false).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<MarketDataFullRunIntent?> GetPendingIntentAsync(
        BrokerEnvironmentKind environment,
        CancellationToken cancellationToken)
    {
        var appliedId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext,
            contextResolver,
            environment,
            cancellationToken,
            requireExecutable: false).ConfigureAwait(false);
        var intent = await dbContext.MarketDataFullRunIntents.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == appliedId, cancellationToken)
            .ConfigureAwait(false);
        if (intent is null)
        {
            return null;
        }

        if (!Enum.TryParse<MarketDataFullRunTrigger>(intent.Trigger, out var trigger))
        {
            throw new InvalidOperationException("The persisted full-run intent has an unsupported trigger.");
        }

        return new(
            trigger,
            intent.CollectionConfigurationVersion,
            intent.InterestRevision,
            intent.UpdatedAtUtc);
    }

    public async Task<MarketDataFailedItemRetry?> GetPendingFailedItemRetryAsync(
        BrokerEnvironmentKind environment,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(environment) || nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A supported broker environment and UTC instant are required.");
        }

        var appliedId = await EfMarketCategoryInstrumentEnvironmentResolver.ResolveAppliedIdAsync(
            dbContext,
            contextResolver,
            environment,
            cancellationToken,
            requireExecutable: false).ConfigureAwait(false);
        var expiredRetries = await dbContext.MarketDataFullRuns
            .Where(item => item.BrokerEnvironmentId == appliedId
                && item.FailedItemFollowUpDueAtUtc != null
                && item.FailedItemFollowUpRunId == null
                && item.FailedItemFollowUpCancelledAtUtc == null
                && item.WindowEndUtc <= nowUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var expiredRetry in expiredRetries)
        {
            expiredRetry.FailedItemFollowUpCancelledAtUtc = nowUtc;
            expiredRetry.UpdatedAtUtc = nowUtc;
        }

        if (expiredRetries.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var pending = await dbContext.MarketDataFullRuns.AsNoTracking()
            .Where(item => item.BrokerEnvironmentId == appliedId
                && item.Status == "Completed"
                && item.Trigger != MarketDataFullRunTrigger.FailedItemRetry.ToString()
                && item.FailedItemFollowUpDueAtUtc <= nowUtc
                && item.FailedItemFollowUpRunId == null
                && item.FailedItemFollowUpCancelledAtUtc == null
                && item.WindowEndUtc > nowUtc)
            .OrderBy(item => item.FailedItemFollowUpDueAtUtc)
            .ThenBy(item => item.RunId)
            .Select(item => new
            {
                item.RunId,
                item.EndpointProfile,
                item.TradingDay,
                item.DetailScheduledSlot,
                item.FailedItemFollowUpDueAtUtc,
                item.WindowEndUtc,
                item.ScheduleRevision,
                item.EffectiveUpdatesPerDay,
                item.CollectionConfigurationVersion,
                item.InterestRevision
            })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (pending is null || pending.FailedItemFollowUpDueAtUtc is not { } dueAtUtc)
        {
            return null;
        }

        var selectedCategories = await dbContext.MarketDataFullRunCategories.AsNoTracking()
            .Where(item => item.RunId == pending.RunId)
            .OrderBy(item => item.CategoryCode)
            .Select(item => item.CategoryCode)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new(
            pending.RunId,
            environment,
            appliedId,
            pending.EndpointProfile,
            pending.TradingDay,
            pending.DetailScheduledSlot ?? 0,
            dueAtUtc,
            pending.WindowEndUtc,
            pending.ScheduleRevision,
            pending.EffectiveUpdatesPerDay,
            pending.CollectionConfigurationVersion,
            pending.InterestRevision,
            selectedCategories);
    }

    private async Task<bool> RecordAttemptAsync(
        MarketDataFullRunLease lease,
        MarketDataFullRunStage stage,
        string? itemCode,
        string status,
        DateTimeOffset nowUtc,
        bool succeeded,
        string? safeReasonCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (!Enum.IsDefined(stage)
            || !StageStatuses.Contains(status)
            || nowUtc.Offset != TimeSpan.Zero
            || !IsSafeReasonCode(safeReasonCode)
            || (succeeded && status != "Succeeded")
            || (!succeeded && status == "Succeeded"))
        {
            throw new ArgumentException("A valid stage/item outcome and safe reason code are required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken).ConfigureAwait(false);
        await MarketCategoryInstrumentSqlLock.AcquireAsync(
            dbContext,
            $"MarketDataFullRuns/{lease.AppliedBrokerEnvironmentId:N}",
            "Exclusive",
            cancellationToken).ConfigureAwait(false);
        var run = await FindOwnedRunAsync(lease, cancellationToken).ConfigureAwait(false);
        if (run is null || run.LeaseExpiresAtUtc <= nowUtc)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        if (itemCode is null)
        {
            var stageEntity = await dbContext.MarketDataFullRunStages
                .SingleOrDefaultAsync(
                    item => item.RunId == lease.RunId && item.Stage == stage.ToString(),
                    cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The full-run stage record is missing.");
            if (stageEntity.Attempts >= MaximumAttempts)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            stageEntity.Attempts++;
            stageEntity.Status = status;
            stageEntity.SafeReasonCode = safeReasonCode;
            stageEntity.UpdatedAtUtc = nowUtc;
            if (succeeded)
            {
                stageEntity.LastSuccessAtUtc = nowUtc;
                run.LastSuccessAtUtc = nowUtc;
                run.CurrentStage = GetNextStage(stage).ToString();
            }
        }
        else
        {
            var item = await dbContext.MarketDataFullRunItems.SingleOrDefaultAsync(
                entry => entry.RunId == lease.RunId
                    && entry.Stage == stage.ToString()
                    && entry.ItemCode == itemCode,
                cancellationToken).ConfigureAwait(false);
            if (item is null)
            {
                item = new MarketDataFullRunItemEntity
                {
                    RunId = lease.RunId,
                    Stage = stage.ToString(),
                    ItemCode = itemCode,
                    UpdatedAtUtc = nowUtc
                };
                dbContext.MarketDataFullRunItems.Add(item);
            }
            else if (item.Attempts >= MaximumAttempts)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            item.Attempts++;
            item.Status = status;
            item.SafeReasonCode = safeReasonCode;
            item.UpdatedAtUtc = nowUtc;
            if (succeeded)
            {
                item.LastSuccessAtUtc = nowUtc;
                run.LastSuccessAtUtc = nowUtc;
            }
        }

        run.UpdatedAtUtc = nowUtc;
        await EfMarketCategoryInstrumentEnvironmentResolver.VerifyAppliedAtCommitAsync(
            dbContext,
            contextResolver,
            lease.AppliedBrokerEnvironmentId,
            lease.Environment,
            lease.EndpointProfile,
            cancellationToken).ConfigureAwait(false);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task UpsertIntentAsync(
        Guid environmentId,
        MarketDataFullRunTrigger trigger,
        long configurationVersion,
        long interestRevision,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.MarketDataFullRunIntents
            .SingleOrDefaultAsync(item => item.BrokerEnvironmentId == environmentId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            dbContext.MarketDataFullRunIntents.Add(new MarketDataFullRunIntentEntity
            {
                BrokerEnvironmentId = environmentId,
                Trigger = trigger.ToString(),
                CollectionConfigurationVersion = configurationVersion,
                InterestRevision = interestRevision,
                UpdatedAtUtc = updatedAtUtc
            });
            return;
        }

        if (updatedAtUtc >= entity.UpdatedAtUtc)
        {
            entity.Trigger = trigger.ToString();
            entity.UpdatedAtUtc = updatedAtUtc;
        }

        entity.CollectionConfigurationVersion = Math.Max(
            entity.CollectionConfigurationVersion,
            configurationVersion);
        entity.InterestRevision = Math.Max(entity.InterestRevision, interestRevision);
    }

    private async Task AddActiveRunCoverageAsync(
        MarketDataFullRunEntity run,
        MarketDataFullRunAdmissionRequest request,
        CancellationToken cancellationToken)
    {
        var eligibleSlots = request.CoveredSlots
            .Where(slot => slot.TradingDay == run.TradingDay
                && slot.ScheduleRevision == run.ScheduleRevision)
            .ToArray();
        if (eligibleSlots.Length == 0)
        {
            return;
        }

        var existingSlots = await dbContext.MarketDataFullRunSlotCoverages
            .Where(item => item.BrokerEnvironmentId == run.BrokerEnvironmentId
                && item.TradingDay == run.TradingDay
                && item.ScheduleRevision == run.ScheduleRevision)
            .Select(item => item.ScheduledSlot)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var coveredSlots = existingSlots.ToHashSet();
        dbContext.MarketDataFullRunSlotCoverages.AddRange(eligibleSlots
            .Where(slot => !coveredSlots.Contains(slot.ScheduledSlot))
            .Select(slot => new MarketDataFullRunSlotCoverageEntity
            {
                BrokerEnvironmentId = run.BrokerEnvironmentId,
                TradingDay = slot.TradingDay,
                ScheduleRevision = slot.ScheduleRevision,
                ScheduledSlot = slot.ScheduledSlot,
                RunId = run.RunId,
                CoverageKind = "ActiveRun",
                CoveredAtUtc = request.AdmittedAtUtc
            }));
    }

    private async Task<MarketDataFullRunEntity?> FindOwnedRunAsync(
        MarketDataFullRunLease lease,
        CancellationToken cancellationToken) =>
        await dbContext.MarketDataFullRuns.SingleOrDefaultAsync(
            item => item.RunId == lease.RunId
                && item.BrokerEnvironmentId == lease.AppliedBrokerEnvironmentId
                && item.Status == "Running"
                && item.LeaseOwner == lease.LeaseOwner
                && item.LeaseFence == lease.LeaseFence,
            cancellationToken).ConfigureAwait(false);

    private async Task<MarketDataFullRunLease> CreateLeaseAsync(
        MarketDataFullRunEntity entity,
        BrokerEnvironmentKind environment,
        Guid owner,
        long fence,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MarketDataFullRunTrigger>(entity.Trigger, out var trigger))
        {
            throw new InvalidOperationException("The persisted full-run trigger is unsupported.");
        }

        var selectedCodes = await dbContext.MarketDataFullRunCategories.AsNoTracking()
            .Where(item => item.RunId == entity.RunId)
            .OrderBy(item => item.CategoryCode)
            .Select(item => item.CategoryCode)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var coveredSlots = await dbContext.MarketDataFullRunSlotCoverages.AsNoTracking()
            .Where(item => item.RunId == entity.RunId)
            .OrderBy(item => item.TradingDay)
            .ThenBy(item => item.ScheduledSlot)
            .Select(item => new MarketDataFullRunSlotIdentity(
                item.TradingDay,
                item.ScheduleRevision,
                item.ScheduledSlot))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return new(
            entity.RunId,
            environment,
            entity.BrokerEnvironmentId,
            entity.EndpointProfile,
            entity.TradingDay,
            entity.AdmittedAtUtc,
            entity.WindowEndUtc,
            entity.ScheduleRevision,
            entity.EffectiveUpdatesPerDay,
            entity.CollectionConfigurationVersion,
            entity.InterestRevision,
            trigger,
            selectedCodes,
            owner,
            fence,
            entity.LeaseExpiresAtUtc
                ?? throw new InvalidOperationException("The persisted full-run lease expiry is missing."),
            coveredSlots,
            entity.DetailScheduledSlot);
    }

    private static void ValidateAdmissionRequest(MarketDataFullRunAdmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.AppliedBrokerEnvironmentId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.EndpointProfile)
            || request.EndpointProfile.Length > 128
            || request.AdmittedAtUtc.Offset != TimeSpan.Zero
            || request.WindowEndUtc.Offset != TimeSpan.Zero
            || request.ScheduleRevision < 1
            || request.EffectiveUpdatesPerDay < 0
            || request.CollectionConfigurationVersion < 1
            || request.InterestRevision < 0
            || !Enum.IsDefined(request.Trigger)
            || request.LeaseOwner == Guid.Empty
            || request.LeaseDuration <= TimeSpan.Zero
            || request.DetailScheduledSlot is < 0
            || (request.Trigger is MarketDataFullRunTrigger.FailedItemRetry) != (request.RetrySourceRunId is not null))
        {
            throw new ArgumentException("A valid applied environment, versioned schedule, window, trigger, and lease are required.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.SelectedCategoryCodes);
        ArgumentNullException.ThrowIfNull(request.CoveredSlots);
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var code in request.SelectedCategoryCodes)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length > 128 || !selected.Add(code))
            {
                throw new ArgumentException("Selected full-run categories must have unique, valid codes.", nameof(request));
            }
        }

        var slots = new HashSet<int>();
        foreach (var slot in request.CoveredSlots)
        {
            if (slot is null
                || slot.TradingDay != request.TradingDay
                || slot.ScheduleRevision != request.ScheduleRevision
                || slot.ScheduledSlot < 0
                || !slots.Add(slot.ScheduledSlot))
            {
                throw new ArgumentException("Covered slots must be unique and match the run trading day and schedule revision.", nameof(request));
            }
        }
    }

    private static void ValidateLease(
        MarketDataFullRunLease lease,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (lease.RunId == Guid.Empty
            || lease.AppliedBrokerEnvironmentId == Guid.Empty
            || lease.LeaseOwner == Guid.Empty
            || lease.LeaseFence < 1
            || nowUtc.Offset != TimeSpan.Zero
            || leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentException("A valid full-run lease renewal request is required.");
        }
    }

    private static bool IsSafeReasonCode(string? value) =>
        value is null
        || value.Length <= 128
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static string GetCoverageKind(MarketDataFullRunTrigger trigger) =>
        trigger switch
        {
            MarketDataFullRunTrigger.Scheduled => "Scheduled",
            MarketDataFullRunTrigger.CatchUp => "CatchUp",
            MarketDataFullRunTrigger.Manual => "Manual",
            MarketDataFullRunTrigger.Interest => "Interest",
            MarketDataFullRunTrigger.Configuration => "Configuration",
            MarketDataFullRunTrigger.FailedItemRetry => "FailedItemRetry",
            _ => throw new ArgumentOutOfRangeException(nameof(trigger))
        };

    private static MarketDataFullRunStage GetNextStage(MarketDataFullRunStage stage) =>
        stage switch
        {
            MarketDataFullRunStage.Categories => MarketDataFullRunStage.Listings,
            MarketDataFullRunStage.Listings => MarketDataFullRunStage.Details,
            _ => MarketDataFullRunStage.Details
        };
}
