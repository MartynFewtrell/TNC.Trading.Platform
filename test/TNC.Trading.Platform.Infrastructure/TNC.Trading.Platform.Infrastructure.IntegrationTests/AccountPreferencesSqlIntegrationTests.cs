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

    private static TrailingStopsPreferenceObservation CreateObservation(DateTimeOffset observedAt, Guid? id = null) => new(id ?? Guid.NewGuid(), true, observedAt, observedAt, PlatformEnvironmentKind.Test, BrokerEnvironmentKind.Demo, "Observed", "AccountPreferences", "integration", Guid.NewGuid().ToString("N"));
    private static TrailingStopsPreferenceObservationEntity CreateEntity(DateTimeOffset observedAt) => new()
    {
        TrailingStopsPreferenceObservationId = Guid.NewGuid(), BrokerEnvironment = "Demo", PlatformEnvironment = "Test", TrailingStopsEnabled = true,
        ObservedAtUtc = observedAt, RecordedAtUtc = observedAt, ObservationKind = "Observed", Source = "AccountPreferences", CorrelationId = Guid.NewGuid().ToString("N")
    };
}