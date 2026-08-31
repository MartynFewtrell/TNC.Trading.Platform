using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AccountPreferences;
using TNC.Trading.Platform.Infrastructure.Operations.Retention;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class AccountPreferencesSqlIntegrationTests(SqlServerDatabaseFixture fixture)
{
    /// <summary>Verifies the AccountPreferences migration creates the table and its deterministic keyset index.</summary>
    [Fact]
    public async Task MigrateAsync_ShouldCreateAccountPreferencesTableAndIndex_WhenDatabaseIsEmpty()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var tables = await context.Database.SqlQueryRaw<string>("SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TrailingStopsPreferenceObservations'").ToListAsync();
        var indexes = await context.Database.SqlQueryRaw<string>("SELECT name AS [Value] FROM sys.indexes WHERE object_id = OBJECT_ID(N'TrailingStopsPreferenceObservations')").ToListAsync(fixture.CancellationToken);
        var migrations = await context.Database.SqlQueryRaw<string>("SELECT MigrationId AS [Value] FROM __EFMigrationsHistory").ToListAsync(fixture.CancellationToken);
        Assert.Contains("TrailingStopsPreferenceObservations", tables);
        Assert.Contains("IX_TrailingStopsPreferenceObservations_BrokerEnvironment_PlatformEnvironment_ObservedAtUtc_TrailingStopsPreferenceObservationId", indexes);
        Assert.DoesNotContain("IX_TrailingStopsPreferenceObservations_BrokerEnvironment_ObservedAtUtc_TrailingStopsPreferenceObservationId", indexes);
        Assert.Contains("20260830112227_SnapshotRepair", migrations);
        Assert.Contains("20260830160041_RemoveTrailingStopsBrokerEnvironmentIndex", migrations);
        Assert.DoesNotContain("20260830120000_AddTrailingStopsPreferenceObservations", migrations);
    }

    /// <summary>Verifies rerunning migration against an applied SnapshotRepair schema does not issue a duplicate table creation.</summary>
    [Fact]
    public async Task MigrateAsync_ShouldNotRecreateObservationTable_WhenSnapshotRepairIsAlreadyApplied()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var before = await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = 'TrailingStopsPreferenceObservations'").SingleAsync(fixture.CancellationToken);

        await context.Database.MigrateAsync(fixture.CancellationToken);

        var after = await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name = 'TrailingStopsPreferenceObservations'").SingleAsync(fixture.CancellationToken);
        Assert.Equal(1, before);
        Assert.Equal(1, after);
    }

    /// <summary>Verifies observations remain readable after the writing SQL context is replaced.</summary>
    [Fact]
    public async Task ListAsync_ShouldReadObservationAfterContextRestart_WhenObservationWasSaved()
    {
        await fixture.ResetDatabaseAsync();
        var observation = CreateObservation(DateTimeOffset.UtcNow);
        await using (var writingContext = fixture.CreateDbContext())
        {
            await writingContext.Database.MigrateAsync(fixture.CancellationToken);
            await new EfTrailingStopsPreferenceObservationStore(writingContext).AppendAsync(observation, fixture.CancellationToken);
        }
        await using var restartedContext = fixture.CreateDbContext();
        var result = await new EfTrailingStopsPreferenceObservationStore(restartedContext).ListAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, null, 10, fixture.CancellationToken);
        Assert.Equal(observation.Id, Assert.Single(result.Observations).Id);
    }

    /// <summary>Verifies equal timestamps page by descending ID without duplicates or missing rows.</summary>
    [Fact]
    public async Task ListAsync_ShouldPageEqualTimestampsDeterministically_WhenRowsShareTimestamp()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var observedAt = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        Guid[] seededIds =
        [
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-010000000000"),
            Guid.Parse("00000001-0000-0000-0000-000000000000"),
            Guid.Parse("01000000-0000-0000-0000-000000000000")
        ];
        var store = new EfTrailingStopsPreferenceObservationStore(context);
        foreach (var seededId in seededIds)
        {
            await store.AppendAsync(CreateObservation(observedAt, seededId), fixture.CancellationToken);
        }

        var expectedIds = await context.TrailingStopsPreferenceObservations
            .AsNoTracking()
            .Where(item => item.PlatformEnvironment == "Test" && item.BrokerEnvironment == "Demo")
            .OrderByDescending(item => item.ObservedAtUtc)
            .ThenByDescending(item => item.TrailingStopsPreferenceObservationId)
            .Select(item => item.TrailingStopsPreferenceObservationId)
            .ToListAsync(fixture.CancellationToken);
        Assert.False(expectedIds.SequenceEqual(seededIds.OrderByDescending(id => id)));

        var actualIds = new List<Guid>();
        string? cursor = null;
        var pageCount = 0;
        do
        {
            var page = await store.ListAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, cursor, 2, fixture.CancellationToken);
            actualIds.AddRange(page.Observations.Select(observation => observation.Id));
            cursor = page.NextCursor;
            pageCount++;
            Assert.True(pageCount <= seededIds.Length, "Keyset paging did not terminate within the seeded row count.");
        }
        while (cursor is not null);

        Assert.Null(cursor);
        Assert.Equal(seededIds.Length, actualIds.Count);
        Assert.Equal(seededIds.Length, actualIds.Distinct().Count());
        Assert.Equal(expectedIds, actualIds);
    }

    /// <summary>Verifies platform and broker partitions cannot leak observations into the selected history.</summary>
    [Fact]
    public async Task ListAsync_ShouldIsolateSelectedPartition_WhenOtherPartitionsContainRows()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var selected = CreateObservation(DateTimeOffset.UtcNow);
        var other = CreateEntity(DateTimeOffset.UtcNow);
        other.PlatformEnvironment = "Production";
        other.BrokerEnvironment = "Live";
        context.TrailingStopsPreferenceObservations.AddRange(
            new TrailingStopsPreferenceObservationEntity
            {
                TrailingStopsPreferenceObservationId = selected.Id, TrailingStopsEnabled = selected.TrailingStopsEnabled,
                ObservedAtUtc = selected.ObservedAtUtc, RecordedAtUtc = selected.RecordedAtUtc, PlatformEnvironment = "Test",
                BrokerEnvironment = "Demo", ObservationKind = selected.ObservationKind, Source = selected.Source,
                Actor = selected.Actor, CorrelationId = selected.CorrelationId
            }, other);
        await context.SaveChangesAsync(fixture.CancellationToken);

        var result = await new EfTrailingStopsPreferenceObservationStore(context).ListAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, null, 10, fixture.CancellationToken);

        Assert.Equal([selected.Id], result.Observations.Select(observation => observation.Id));
    }

    /// <summary>Verifies malformed, unknown, and cross-partition cursors are rejected instead of silently restarting page one.</summary>
    [Fact]
    public async Task ListAsync_ShouldRejectInvalidCursors_WhenCursorIsMalformedUnknownOrOutsidePartition()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var selected = CreateObservation(DateTimeOffset.UtcNow);
        await new EfTrailingStopsPreferenceObservationStore(context).AppendAsync(selected, fixture.CancellationToken);
        var other = CreateEntity(DateTimeOffset.UtcNow);
        other.PlatformEnvironment = "Production";
        other.BrokerEnvironment = "Live";
        context.TrailingStopsPreferenceObservations.Add(other);
        await context.SaveChangesAsync(fixture.CancellationToken);
        var store = new EfTrailingStopsPreferenceObservationStore(context);

        Assert.True((await store.ListAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "not-a-guid", 10, fixture.CancellationToken)).HasInvalidCursor);
        Assert.True((await store.ListAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, Guid.NewGuid().ToString(), 10, fixture.CancellationToken)).HasInvalidCursor);
        Assert.True((await store.ListAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, other.TrailingStopsPreferenceObservationId.ToString(), 10, fixture.CancellationToken)).HasInvalidCursor);
    }

    /// <summary>Verifies SQL retention removes expired account-preference observations while preserving current history.</summary>
    [Fact]
    public async Task ApplyAsync_ShouldRetainCurrentAccountPreferenceObservation_WhenExpiredRowsExist()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        context.TrailingStopsPreferenceObservations.AddRange(
            CreateEntity(DateTimeOffset.UtcNow.AddDays(-91)), CreateEntity(DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(fixture.CancellationToken);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Retention:OperationalRecordsDays"] = "1" }).Build();
        var processor = new OperationalRecordRetentionProcessor(context, configuration, TimeProvider.System, NullLogger<OperationalRecordRetentionProcessor>.Instance);
        Assert.Equal(1, await processor.ApplyAsync(fixture.CancellationToken));
        Assert.Single(await context.TrailingStopsPreferenceObservations.ToListAsync(fixture.CancellationToken));
    }

    /// <summary>Trace: Phase 2.3. Verifies a migrated database starts without inferred operator intent.</summary>
    [Fact]
    public async Task GetAsync_ShouldReturnUnconfigured_WhenDatabaseHasNoDesiredState()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);

        var state = await new EfAccountPreferencesCurrentStateStore(context).GetAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, fixture.CancellationToken);

        Assert.Null(state);
    }

    /// <summary>Trace: Phase 2.3. Verifies desired state, typed actor/correlation audit data, and revision survive a context restart.</summary>
    [Fact]
    public async Task CommitDesiredStateAsync_ShouldPersistStateAndTypedAudit_WhenContextRestarts()
    {
        await fixture.ResetDatabaseAsync();
        var change = CreateChange(true, "operator", "correlation-1");
        await using (var writingContext = fixture.CreateDbContext())
        {
            await writingContext.Database.MigrateAsync(fixture.CancellationToken);
            var result = await new EfAccountPreferencesCurrentStateStore(writingContext).CommitDesiredStateAsync(change, fixture.CancellationToken);
            Assert.True(result.Committed);
            Assert.Equal(1, result.Revision);
        }

        await using var restartedContext = fixture.CreateDbContext();
        var state = await new EfAccountPreferencesCurrentStateStore(restartedContext).GetAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, fixture.CancellationToken);
        var audit = await restartedContext.AccountPreferencesDesiredStateAudits.AsNoTracking().SingleAsync(fixture.CancellationToken);
        Assert.Equal(true, state!.DesiredTrailingStopsEnabled);
        Assert.Equal(1, state.DesiredRevision);
        Assert.Equal("operator", audit.Actor);
        Assert.Equal("correlation-1", audit.CorrelationId);
    }

    /// <summary>Trace: Phase 2.3. Verifies compare-and-set rejects stale desired revisions without changing persisted intent.</summary>
    [Fact]
    public async Task CommitDesiredStateAsync_ShouldRejectStaleRevision_WhenAnotherChangeWasCommitted()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var store = new EfAccountPreferencesCurrentStateStore(context);
        await store.CommitDesiredStateAsync(CreateChange(true, "first", "correlation-1"), fixture.CancellationToken);

        var stale = await store.CommitDesiredStateAsync(CreateChange(false, "stale", "correlation-2", expectedRevision: null), fixture.CancellationToken);
        var current = await store.GetAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, fixture.CancellationToken);

        Assert.True(stale.Committed);
        Assert.Equal(2, stale.Revision);
        Assert.Equal(false, current!.DesiredTrailingStopsEnabled);
    }

    /// <summary>Trace: Phase 2.3. Verifies due work is restricted to committed states whose retry time is ready.</summary>
    [Fact]
    public async Task ClaimDueWorkAsync_ShouldReturnOnlyReadyDesiredStates_WhenRetryTimesDiffer()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var store = new EfAccountPreferencesCurrentStateStore(context);
        await store.CommitDesiredStateAsync(CreateChange(true, "operator", "due"), fixture.CancellationToken);
        await store.NudgeAuthenticationAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "account-1", DateTimeOffset.UtcNow.AddMinutes(-1), fixture.CancellationToken);

        var due = await store.ClaimDueWorkAsync(DateTimeOffset.UtcNow, 10, fixture.CancellationToken);

        Assert.Single(due);
        Assert.Equal("account-1", due[0].AccountId);
    }

    /// <summary>Trace: Phase 2.3. Verifies the account/environment application lock excludes a competing SQL session.</summary>
    [Fact]
    public async Task AcquireAsync_ShouldRejectContentionForSameAccount_WhenAnotherSessionOwnsLease()
    {
        await fixture.ResetDatabaseAsync();
        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();
        await firstContext.Database.MigrateAsync(fixture.CancellationToken);
        var first = new SqlAccountPreferencesReconciliationLease(firstContext);
        var second = new SqlAccountPreferencesReconciliationLease(secondContext);
        await using var held = await first.AcquireAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "account-1", fixture.CancellationToken);

        var contender = await second.AcquireAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "account-1", fixture.CancellationToken);

        Assert.NotNull(held);
        Assert.Null(contender);
    }

    /// <summary>Trace: Phase 2.3. Verifies a completion for an older desired revision cannot overwrite newer intent.</summary>
    [Fact]
    public async Task CompleteReconciliationAsync_ShouldRejectStaleRevision_WhenDesiredStateChanged()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        var store = new EfAccountPreferencesCurrentStateStore(context);
        var first = await store.CommitDesiredStateAsync(CreateChange(true, "first", "first"), fixture.CancellationToken);
        await store.CommitDesiredStateAsync(CreateChange(false, "second", "second", first.Revision), fixture.CancellationToken);
        var staleState = first.State with { ObservedTrailingStopsEnabled = true, VerificationStatus = AccountPreferencesVerificationStatus.InSync };

        var completion = await store.CompleteReconciliationAsync(first.State.Id, first.Revision, staleState, fixture.CancellationToken);
        var current = await store.GetAsync(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, fixture.CancellationToken);

        Assert.False(completion.Applied);
        Assert.Equal(2, current!.DesiredRevision);
        Assert.Equal(false, current.DesiredTrailingStopsEnabled);
    }

    /// <summary>Trace: Phase 2.3. Verifies generic operational retention does not remove desired-state audit history.</summary>
    [Fact]
    public async Task ApplyAsync_ShouldPreserveDesiredStateAudit_WhenOperationalRetentionRuns()
    {
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync(fixture.CancellationToken);
        await new EfAccountPreferencesCurrentStateStore(context).CommitDesiredStateAsync(CreateChange(true, "operator", "audit"), fixture.CancellationToken);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Retention:OperationalRecordsDays"] = "0" }).Build();
        var processor = new OperationalRecordRetentionProcessor(context, configuration, TimeProvider.System, NullLogger<OperationalRecordRetentionProcessor>.Instance);

        await processor.ApplyAsync(fixture.CancellationToken);

        Assert.Single(await context.AccountPreferencesDesiredStateAudits.ToListAsync(fixture.CancellationToken));
    }

    private static TrailingStopsPreferenceObservation CreateObservation(DateTimeOffset observedAt, Guid? id = null) => new(id ?? Guid.NewGuid(), true, observedAt, observedAt, PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "account-1", "Observed", "AccountPreferences", "integration", Guid.NewGuid().ToString("N"));
    private static AccountPreferencesDesiredStateChange CreateChange(bool enabled, string actor, string correlationId, long? expectedRevision = null) => new(PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "account-1", enabled, expectedRevision, actor, DateTimeOffset.UtcNow, correlationId);
    private static TrailingStopsPreferenceObservationEntity CreateEntity(DateTimeOffset observedAt) => new()
    {
        TrailingStopsPreferenceObservationId = Guid.NewGuid(), BrokerEnvironment = "Demo", PlatformEnvironment = "Test", TrailingStopsEnabled = true,
        ObservedAtUtc = observedAt, RecordedAtUtc = observedAt, ObservationKind = "Observed", Source = "AccountPreferences", CorrelationId = Guid.NewGuid().ToString("N")
    };
}