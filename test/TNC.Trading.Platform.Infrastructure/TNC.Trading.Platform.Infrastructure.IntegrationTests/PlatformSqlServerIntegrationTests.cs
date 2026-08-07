using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;
using TNC.Trading.Platform.Infrastructure.Platform;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;
using TNC.Trading.Platform.Infrastructure.Operations.Retention;
using TNC.Trading.Platform.Infrastructure.Startup;
using TNC.Trading.Platform.Application.Features.AccountDetails;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class PlatformSqlServerIntegrationTests(SqlServerDatabaseFixture fixture)
{
    /// <summary>
    /// Verifies: the shared Data Protection key ring persists ciphertext compatibility across provider instances,
    /// and a newly generated key does not invalidate values protected by an older key.
    /// Expected: a second provider instance can unprotect the original value after key rotation.
    /// Why: API/Web process replacement and scheduled key rotation must preserve encrypted IG credentials.
    /// </summary>
    [Fact]
    public async Task ProtectAndUnprotect_ShouldSurviveProviderRestartAndKeyRotation_WhenSqlKeyRingIsShared()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        await dbContext.Database.MigrateAsync();

        using var firstProvider = CreateDataProtectionProvider();
        var protector = firstProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Phase10.1.SharedKeyRing");
        var protectedValue = protector.Protect("credential-value");
        var keyManager = firstProvider.GetRequiredService<IKeyManager>();
        keyManager.CreateNewKey(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(91));

        using var restartedProvider = CreateDataProtectionProvider();
        var restartedProtector = restartedProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Phase10.1.SharedKeyRing");

        Assert.Equal("credential-value", restartedProtector.Unprotect(protectedValue));

        ServiceProvider CreateDataProtectionProvider()
        {
            var services = new ServiceCollection();
            services.AddDbContext<PlatformDataProtectionKeyContext>(options => options.UseSqlServer(fixture.ConnectionString));
            services.AddDataProtection()
                .SetApplicationName("TNC.Trading.Platform")
                .SetDefaultKeyLifetime(TimeSpan.FromDays(90))
                .PersistKeysToDbContext<PlatformDataProtectionKeyContext>();

            return services.BuildServiceProvider();
        }
    }

    /// <summary>
    /// Verifies that Infrastructure migrations create every current SQL schema object in an empty database.
    /// This guards the deployed schema lifecycle from regressing to an InMemory-only or EnsureCreated-only contract.
    /// </summary>
    [Fact]
    public async Task MigrateAsync_ShouldCreateCurrentSchema_WhenDatabaseIsEmpty()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();

        await dbContext.Database.MigrateAsync();

        var tables = await dbContext.Database.SqlQueryRaw<string>(
                "SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'")
            .ToListAsync();

        Assert.All(
            new[]
            {
                "AccountDetailsAccounts", "AccountDetailsRetrievals", "AuthRetryCycles", "AuthRuntimeStates", "ConfigurationAudits", "DataProtectionKeys", "IgLoginSnapshots", "IgProofData",
                "NotificationRecords", "OperationalEvents", "PlatformConfigurations", "ProtectedCredentials"
            },
            table => Assert.Contains(table, tables));
        Assert.Contains("20260807103334_AddAccountDetailsSnapshots", await dbContext.Database.GetAppliedMigrationsAsync());

        var indexes = await dbContext.Database.SqlQueryRaw<string>("""
            SELECT name AS [Value]
            FROM sys.indexes
            WHERE object_id IN (OBJECT_ID(N'AccountDetailsRetrievals'), OBJECT_ID(N'AccountDetailsAccounts'))
            """).ToListAsync();
        Assert.Contains("IX_AccountDetailsRetrievals_BrokerEnvironment_RetrievedAtUtc_AccountDetailsRetrievalId", indexes);
        Assert.Contains("IX_AccountDetailsRetrievals_BrokerEnvironment_TradingDay", indexes);
        Assert.Contains("IX_AccountDetailsAccounts_AccountDetailsRetrievalId_AccountId", indexes);
    }

    /// <summary>
    /// Verifies Account Details refresh ownership is an environment-scoped SQL application lock.
    /// Expected: the same environment contends, a different environment proceeds, and release permits reacquisition.
    /// Why: replicas must coordinate refreshes per broker environment without blocking unrelated environments.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ShouldScopeContentionByEnvironmentAndReleaseOwnership_WhenSqlSessionsCompete()
    {
        await fixture.ResetDatabaseAsync();
        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();
        await firstContext.Database.MigrateAsync();
        var firstLeaseProvider = new SqlAccountDetailsRefreshLease(firstContext);
        var secondLeaseProvider = new SqlAccountDetailsRefreshLease(secondContext);

        var firstLease = await firstLeaseProvider.AcquireAsync(BrokerEnvironmentKind.Demo, AccountDetailsTriggerSource.Manual, CancellationToken.None);
        var sameEnvironment = await secondLeaseProvider.AcquireAsync(BrokerEnvironmentKind.Demo, AccountDetailsTriggerSource.Manual, CancellationToken.None);
        var otherEnvironment = await secondLeaseProvider.AcquireAsync(BrokerEnvironmentKind.Live, AccountDetailsTriggerSource.Manual, CancellationToken.None);

        Assert.False(sameEnvironment.Acquired);
        Assert.True(otherEnvironment.Acquired);
        await otherEnvironment.DisposeAsync();
        await firstLease.DisposeAsync();

        var recovered = await secondLeaseProvider.AcquireAsync(BrokerEnvironmentKind.Demo, AccountDetailsTriggerSource.Manual, CancellationToken.None);
        Assert.True(recovered.Acquired);
        await recovered.DisposeAsync();
    }

    /// <summary>
    /// Verifies manual contention reports the latest saved retrieval, while automatic daily capture yields without a timestamp.
    /// Expected: both requests are denied by the same SQL lease, with only the manual result carrying the latest timestamp.
    /// Why: operators need conflict context, while automatic capture remains best-effort and non-blocking.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ShouldReturnManualLatestButYieldAutomatic_WhenEnvironmentLeaseIsHeld()
    {
        await fixture.ResetDatabaseAsync();
        await using var seedContext = fixture.CreateDbContext();
        await seedContext.Database.MigrateAsync();
        var retrievedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        seedContext.AccountDetailsRetrievals.Add(new AccountDetailsRetrievalEntity
        {
            AccountDetailsRetrievalId = Guid.NewGuid(), BrokerEnvironment = BrokerEnvironmentKind.Demo.ToString(), RetrievedAtUtc = retrievedAt,
            TradingDay = DateOnly.FromDateTime(DateTime.UtcNow), AccountCount = 0, TriggerSource = AccountDetailsTriggerSource.Manual.ToString()
        });
        await seedContext.SaveChangesAsync();

        await using var ownerContext = fixture.CreateDbContext();
        await using var contenderContext = fixture.CreateDbContext();
        var owner = new SqlAccountDetailsRefreshLease(ownerContext);
        var contender = new SqlAccountDetailsRefreshLease(contenderContext);
        await using var held = await owner.AcquireAsync(BrokerEnvironmentKind.Demo, AccountDetailsTriggerSource.Manual, CancellationToken.None);

        var manual = await contender.AcquireAsync(BrokerEnvironmentKind.Demo, AccountDetailsTriggerSource.Manual, CancellationToken.None);
        var automatic = await contender.AcquireAsync(BrokerEnvironmentKind.Demo, AccountDetailsTriggerSource.Automatic, CancellationToken.None);

        Assert.False(manual.Acquired);
        Assert.Equal(retrievedAt, manual.LatestRetrievedAtUtc);
        Assert.False(automatic.Acquired);
        Assert.Null(automatic.LatestRetrievedAtUtc);
    }

    /// <summary>
    /// Verifies snapshot persistence is atomic across the retrieval parent and account children.
    /// Expected: a duplicate child account key causes SaveAsync to fail and leaves neither parent nor child rows committed.
    /// Why: partial account snapshots would make immutable history internally inconsistent.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ShouldRollbackRetrievalAndChildren_WhenChildPersistenceFails()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        await dbContext.Database.MigrateAsync();
        var store = new EfAccountDetailsSnapshotStore(dbContext);
        var snapshot = new AccountDetailsSnapshot(
            Guid.NewGuid(), BrokerEnvironmentKind.Demo, DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), AccountDetailsTriggerSource.Manual,
            [CreateAccount("DUPLICATE"), CreateAccount("DUPLICATE")]);

        await Assert.ThrowsAsync<DbUpdateException>(() => store.SaveAsync(snapshot, CancellationToken.None));

        Assert.Empty(await dbContext.AccountDetailsRetrievals.AsNoTracking().ToListAsync());
        Assert.Empty(await dbContext.AccountDetailsAccounts.AsNoTracking().ToListAsync());
    }

    /// <summary>
    /// Verifies immutable account retrievals survive a SQL context restart and support deterministic adjacent history reads.
    /// Expected: the latest retrieval contains every child account, and the composite cursor resolves the older snapshot.
    /// Why: Viewer history must remain environment-scoped and must not depend on in-memory state or offset pagination.
    /// </summary>
    [Fact]
    public async Task GetLatestAndAdjacentAsync_ShouldReadImmutableAccountHistoryAfterContextRestart_WhenSnapshotsWereSavedToSqlServer()
    {
        await fixture.ResetDatabaseAsync();
        var firstRetrievedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        var secondRetrievedAtUtc = DateTimeOffset.UtcNow;

        await using (var writingContext = fixture.CreateDbContext())
        {
            await writingContext.Database.MigrateAsync();
            var writingStore = new EfAccountDetailsSnapshotStore(writingContext);
            await writingStore.SaveAsync(
                new AccountDetailsSnapshot(
                    Guid.NewGuid(), BrokerEnvironmentKind.Demo, firstRetrievedAtUtc, DateOnly.FromDateTime(firstRetrievedAtUtc.UtcDateTime),
                    AccountDetailsTriggerSource.Automatic, [CreateAccount("ACC-1")]),
                CancellationToken.None);
            await writingStore.SaveAsync(
                new AccountDetailsSnapshot(
                    Guid.NewGuid(), BrokerEnvironmentKind.Demo, secondRetrievedAtUtc, DateOnly.FromDateTime(secondRetrievedAtUtc.UtcDateTime),
                    AccountDetailsTriggerSource.Manual, [CreateAccount("ACC-1"), CreateAccount("ACC-2")]),
                CancellationToken.None);
        }

        await using var restartedContext = fixture.CreateDbContext();
        var restartedStore = new EfAccountDetailsSnapshotStore(restartedContext);
        var latest = await restartedStore.GetLatestAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal(2, latest.Accounts.Count);
        Assert.Equal(secondRetrievedAtUtc, latest.RetrievedAtUtc);

        var older = await restartedStore.GetBeforeAsync(
            BrokerEnvironmentKind.Demo,
            new AccountDetailsCursor(latest.RetrievedAtUtc, latest.RetrievalId),
            CancellationToken.None);

        Assert.NotNull(older);
        Assert.Equal(firstRetrievedAtUtc, older.RetrievedAtUtc);
        Assert.Single(older.Accounts);
        Assert.Equal("ACC-1", older.Accounts[0].AccountId);
        Assert.Null(await restartedStore.GetBeforeAsync(
            BrokerEnvironmentKind.Demo,
            new AccountDetailsCursor(older.RetrievedAtUtc, older.RetrievalId),
            CancellationToken.None));
    }

    private static AccountDetailsAccount CreateAccount(string accountId) => new(
        accountId, "Integration account", null, "ACTIVE", "CFD", true, 100m, 0m, 0m, 100m, "GBP", true, true);

    /// <summary>
    /// Verifies: latest proof data remains readable through a newly constructed SQL context after the writing context is disposed.
    /// Expected: the second store instance returns the complete snapshot and an update keeps one row per broker environment.
    /// Why: API process replacement must preserve the status proof snapshot through the durable platform database.
    /// </summary>
    [Fact]
    public async Task GetLatestAsync_ShouldReadProofDataAfterContextRestart_WhenSnapshotWasSavedToSqlServer()
    {
        await fixture.ResetDatabaseAsync();
        await using (var writingContext = fixture.CreateDbContext())
        {
            await writingContext.Database.MigrateAsync();
            var writingStore = new EfPlatformIgProofDataStore(writingContext);
            await writingStore.SaveAsync(
                BrokerEnvironmentKind.Demo,
                new IgProofDataSnapshot("Demo Account", "ACC1", 5000m, 2, DateTimeOffset.UtcNow),
                CancellationToken.None);
        }

        await using var restartedContext = fixture.CreateDbContext();
        var restartedStore = new EfPlatformIgProofDataStore(restartedContext);
        var snapshot = await restartedStore.GetLatestAsync(BrokerEnvironmentKind.Demo, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal("Demo Account", snapshot.PreferredAccountName);
        Assert.Equal("ACC1", snapshot.PreferredAccountId);
        Assert.Equal(5000m, snapshot.Balance);
        Assert.Equal(2, snapshot.OpenPositionCount);

        await restartedStore.SaveAsync(
            BrokerEnvironmentKind.Demo,
            snapshot with { Balance = 5100m },
            CancellationToken.None);

        Assert.Equal(5100m, (await restartedStore.GetLatestAsync(BrokerEnvironmentKind.Demo, CancellationToken.None))!.Balance);
        Assert.Equal(1, await restartedContext.IgProofData.CountAsync());
    }

    /// <summary>
    /// Verifies the conservative no-history transition path: a guarded baseline is recorded before migrations
    /// continue, and existing rows remain intact. This matters because local SQL resources may predate migration history.
    /// </summary>
    [Fact]
    public async Task MigrateAsync_ShouldUpgradeWithoutDataLoss_WhenNoHistoryDatabaseIsTransitioned()
    {
        await fixture.ResetDatabaseAsync();
        await using var legacyContext = fixture.CreateDbContext();
        await legacyContext.Database.EnsureCreatedAsync();
        legacyContext.PlatformConfigurations.Add(new PlatformConfigurationEntity
        {
            PlatformEnvironment = "Test",
            BrokerEnvironment = "Demo",
            TradingHoursStart = new TimeOnly(9),
            TradingHoursEnd = new TimeOnly(17),
            TradingDaysCsv = "Monday",
            WeekendBehavior = "Inactive",
            BankHolidayExclusionsJson = "[]",
            TimeZone = "UTC",
            NotificationProvider = "Recorded",
            UpdatedBy = "integration-test"
        });
        await legacyContext.SaveChangesAsync();

        await using var transitionConnection = new SqlConnection(await GetConnectionStringAsync(legacyContext));
        await transitionConnection.OpenAsync();
        await using (var command = transitionConnection.CreateCommand())
        {
            command.CommandText = "DROP TABLE IF EXISTS [DataProtectionKeys];";
            await command.ExecuteNonQueryAsync();
            command.CommandText = "DROP TABLE IF EXISTS [IgProofData];";
            await command.ExecuteNonQueryAsync();
            command.CommandText = "DROP TABLE IF EXISTS [AccountDetailsAccounts];";
            await command.ExecuteNonQueryAsync();
            command.CommandText = "DROP TABLE IF EXISTS [AccountDetailsRetrievals];";
            await command.ExecuteNonQueryAsync();
            command.CommandText = "IF OBJECT_ID('__EFMigrationsHistory', 'U') IS NOT NULL DROP TABLE __EFMigrationsHistory; CREATE TABLE __EFMigrationsHistory (MigrationId nvarchar(150) NOT NULL, ProductVersion nvarchar(32) NOT NULL, CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId))";
            await command.ExecuteNonQueryAsync();
            command.CommandText = "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260727202238_InitialPlatformSchema', '10.0.5')";
            await command.ExecuteNonQueryAsync();
        }

        await using var migratedContext = fixture.CreateDbContext();
        await migratedContext.Database.MigrateAsync();

        Assert.Equal("integration-test", await migratedContext.PlatformConfigurations.Select(item => item.UpdatedBy).SingleAsync());
    }

    /// <summary>
    /// Trace: Clean Architecture Migration Phase 10.6 migration recovery.
    /// Verifies: a migration conflict fails startup closed without deleting the incompatible SQL object or hiding the migrations already committed before the conflict.
    /// Expected: the initializer reports a schema failure, leaves the injected conflicting table and prior migration history intact, and does not run bootstrap configuration; after correction, a fresh retry completes the remaining migrations.
    /// Why: migration recovery must preserve operator evidence and data while preventing readiness from being reported for an unverified schema.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_ShouldFailClosedAndPreserveSchema_WhenMigrationConflictsWithExistingObject()
    {
        await fixture.ResetDatabaseAsync();
        await using (var setupContext = fixture.CreateDbContext())
        {
            await setupContext.Database.ExecuteSqlRawAsync("CREATE TABLE [DataProtectionKeys] ([LegacyValue] nvarchar(32) NOT NULL);");
        }

        await using var failedContext = fixture.CreateDbContext();
        var configurationStore = CreateConfigurationStore(failedContext, CreateConfiguration());
        var initializer = CreateStartupInitializer(failedContext, configurationStore);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => initializer.InitializeAsync(CancellationToken.None));

        Assert.Contains("schema initialization failed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "20260727202238_InitialPlatformSchema",
            Assert.Single(await failedContext.Database.GetAppliedMigrationsAsync()));
        Assert.Contains(
            "DataProtectionKeys",
            await failedContext.Database.SqlQueryRaw<string>(
                "SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DataProtectionKeys'").ToListAsync());
        Assert.Empty(await failedContext.PlatformConfigurations.ToListAsync());

        await using (var correctionConnection = new SqlConnection(fixture.ConnectionString))
        {
            await correctionConnection.OpenAsync();
            await using var correctionCommand = correctionConnection.CreateCommand();
            correctionCommand.CommandText = "DROP TABLE [DataProtectionKeys];";
            await correctionCommand.ExecuteNonQueryAsync();
        }

        await using var recoveredContext = fixture.CreateDbContext();
        var recoveredStore = CreateConfigurationStore(recoveredContext, CreateConfiguration());
        var recoveredInitializer = CreateStartupInitializer(recoveredContext, recoveredStore);

        await recoveredInitializer.InitializeAsync(CancellationToken.None);

        Assert.Equal(4, (await recoveredContext.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Single(await recoveredContext.PlatformConfigurations.ToListAsync());
    }

    /// <summary>
    /// Trace: Clean Architecture Migration Phase 10.6 migration recovery.
    /// Verifies: an incomplete no-history schema is rejected without destructive repair, then a corrected database can be initialized successfully.
    /// Expected: the first attempt preserves the partial table and no history; after the operator removes that table, a retry applies all migrations and bootstrap configuration.
    /// Why: recovery requires explicit operator correction followed by a safe retry, rather than an automatic reset that could destroy unrelated data.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_ShouldRecoverAfterOperatorCorrection_WhenPartialSchemaHasNoMigrationHistory()
    {
        await fixture.ResetDatabaseAsync();
        await using (var setupContext = fixture.CreateDbContext())
        {
            await setupContext.Database.ExecuteSqlRawAsync("CREATE TABLE [AuthRetryCycles] ([RetryCycleId] uniqueidentifier NOT NULL PRIMARY KEY);");
        }

        await using (var failedContext = fixture.CreateDbContext())
        {
            var failedStore = CreateConfigurationStore(failedContext, CreateConfiguration());
            var failedInitializer = CreateStartupInitializer(failedContext, failedStore);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => failedInitializer.InitializeAsync(CancellationToken.None));

            Assert.Contains("partial platform schema", exception.InnerException?.Message ?? exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await failedContext.Database.GetAppliedMigrationsAsync());
            Assert.Contains(
                "AuthRetryCycles",
                await failedContext.Database.SqlQueryRaw<string>(
                    "SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AuthRetryCycles'").ToListAsync());
        }

        await using (var correctionConnection = new SqlConnection(fixture.ConnectionString))
        {
            await correctionConnection.OpenAsync();
            await using var correctionCommand = correctionConnection.CreateCommand();
            correctionCommand.CommandText = "DROP TABLE [AuthRetryCycles];";
            await correctionCommand.ExecuteNonQueryAsync();
        }

        await using var recoveredContext = fixture.CreateDbContext();
        var recoveredStore = CreateConfigurationStore(recoveredContext, CreateConfiguration());
        var recoveredInitializer = CreateStartupInitializer(recoveredContext, recoveredStore);

        await recoveredInitializer.InitializeAsync(CancellationToken.None);

        Assert.NotEmpty(await recoveredContext.Database.GetAppliedMigrationsAsync());
        Assert.Single(await recoveredContext.PlatformConfigurations.ToListAsync());
    }

    /// <summary>
    /// Verifies that SQL transaction rollback removes all local writes when a later write fails.
    /// The existing manual-retry committer is the production consistency boundary; this protects it from partial SQL commits.
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldRollbackAllLocalWrites_WhenOneWriteFails()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        await dbContext.Database.MigrateAsync();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        dbContext.OperationalEvents.Add(new OperationalEventEntity
        {
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Category = "Integration",
            EventType = "BeforeFailure",
            PlatformEnvironment = "Test",
            BrokerEnvironment = "Demo",
            Summary = "Should roll back",
            DetailsJson = "{}"
        });
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlRawAsync("THROW 51000, 'forced integration failure', 1;"));
        await transaction.RollbackAsync();

        Assert.Empty(await dbContext.OperationalEvents.AsNoTracking().ToListAsync());
    }

    /// <summary>
    /// Trace: Phase 10.4 configuration-update and reconciliation consistency.
    /// Verifies: the SQL configuration committer keeps the configuration row, protected credential replacements, and audit record in one local SaveChanges transaction.
    /// Expected: an injected audit-write failure leaves the previously committed configuration and all related local rows unchanged.
    /// Why: a durable operator update must not expose a partially applied secret or audit state to the next reconciliation after a process or database failure.
    /// </summary>
    [Fact]
    public async Task CommitAsync_ShouldRollbackConfigurationCredentialsAndAudit_WhenAuditWriteFails()
    {
        await fixture.ResetDatabaseAsync();
        await using (var seedContext = fixture.CreateDbContext())
        {
            await seedContext.Database.MigrateAsync();
            var seedStore = CreateConfigurationStore(seedContext, CreateConfiguration());
            _ = await seedStore.GetCurrentAsync(CancellationToken.None);
        }

        await using (var failingContext = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseSqlServer(fixture.ConnectionString)
                .AddInterceptors(new FailConfigurationAuditInsertInterceptor())
                .Options))
        {
            var failingStore = CreateConfigurationStore(failingContext, CreateConfiguration());

            await Assert.ThrowsAsync<DbUpdateException>(() => failingStore.CommitAsync(
                CreateConfigurationUpdate(
                    platformEnvironment: "Live",
                    brokerEnvironment: "Demo",
                    provider: "RecordedOnly",
                    emailTo: "updated-owner@example.com",
                    apiKey: "new-api-key",
                    identifier: "new-identifier",
                    password: "new-password",
                    changedBy: "phase-10-4"),
                CancellationToken.None));
        }

        await using var verificationContext = fixture.CreateDbContext();
        var persistedConfiguration = await verificationContext.PlatformConfigurations.SingleAsync();

        Assert.Equal("Test", persistedConfiguration.PlatformEnvironment);
        Assert.Equal("Demo", persistedConfiguration.BrokerEnvironment);
        Assert.Equal("bootstrap", persistedConfiguration.UpdatedBy);
        Assert.Empty(await verificationContext.ProtectedCredentials.ToListAsync());
        Assert.Empty(await verificationContext.ConfigurationAudits.ToListAsync());
    }

    /// <summary>
    /// Verifies: independently constructed SQL contexts cannot acquire the platform reconciliation lease at the same time.
    /// Expected: the second context is rejected while the first session owns the exclusive SQL application lock.
    /// Why: API replicas do not share process memory, so the durable writer boundary must be enforced by SQL Server.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ShouldRejectConcurrentOwner_WhenAnotherSqlSessionOwnsTheLease()
    {
        await fixture.ResetDatabaseAsync();
        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();
        var firstLeaseProvider = new SqlPlatformReconciliationLease(firstContext);
        var secondLeaseProvider = new SqlPlatformReconciliationLease(secondContext);

        await using var firstLease = await firstLeaseProvider.AcquireAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => secondLeaseProvider.AcquireAsync(CancellationToken.None));
    }

    /// <summary>
    /// Verifies: releasing a session-owned SQL application lock permits a new replica session to acquire it.
    /// Expected: the second context acquires the lease after the first context disposes its handle.
    /// Why: a crashed or recycled replica must not strand reconciliation ownership permanently.
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ShouldRecoverAfterOwnerRelease_WhenReplicaSessionEnds()
    {
        await fixture.ResetDatabaseAsync();
        await using var firstContext = fixture.CreateDbContext();
        await using var secondContext = fixture.CreateDbContext();
        var firstLeaseProvider = new SqlPlatformReconciliationLease(firstContext);
        var secondLeaseProvider = new SqlPlatformReconciliationLease(secondContext);

        await using (var firstLease = await firstLeaseProvider.AcquireAsync(CancellationToken.None))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => secondLeaseProvider.AcquireAsync(CancellationToken.None));
        }

        await using var recoveredLease = await secondLeaseProvider.AcquireAsync(CancellationToken.None);
    }

    /// <summary>
    /// Verifies SQL Server set-based retention deletes expired records while preserving current records.
    /// This matters because the provider-specific ExecuteDelete path is not proved by InMemory tests.
    /// </summary>
    [Fact]
    public async Task DeleteExpiredAsync_ShouldPreserveCurrentRecords_WhenSqlServerExecutesRetention()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        await dbContext.Database.MigrateAsync();

        var now = DateTimeOffset.UtcNow;
        dbContext.OperationalEvents.AddRange(
            new OperationalEventEntity
            {
                OccurredAtUtc = now.AddDays(-2),
                Category = "Integration",
                EventType = "Expired",
                PlatformEnvironment = "Test",
                BrokerEnvironment = "Demo",
                Summary = "Expired",
                DetailsJson = "{}"
            },
            new OperationalEventEntity
            {
                OccurredAtUtc = now,
                Category = "Integration",
                EventType = "Current",
                PlatformEnvironment = "Test",
                BrokerEnvironment = "Demo",
                Summary = "Current",
                DetailsJson = "{}"
            });
        await dbContext.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Retention:OperationalRecordsDays"] = "1" })
            .Build();
        var processor = new OperationalRecordRetentionProcessor(
            dbContext,
            configuration,
            TimeProvider.System,
            NullLogger<OperationalRecordRetentionProcessor>.Instance);

        var deletedCount = await processor.ApplyAsync(CancellationToken.None);

        Assert.Equal(1, deletedCount);
        Assert.Equal("Current", await dbContext.OperationalEvents.Select(item => item.EventType).SingleAsync());
    }

    private static async Task<string> GetConnectionStringAsync(PlatformDbContext dbContext)
    {
        return dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The SQL Server test context has no connection string.");
    }

    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:PlatformEnvironment"] = "Test",
            ["Bootstrap:BrokerEnvironment"] = "Demo",
            ["Bootstrap:TradingSchedule:StartOfDay"] = "09:00",
            ["Bootstrap:TradingSchedule:EndOfDay"] = "17:00",
            ["Bootstrap:TradingSchedule:TradingDays:0"] = "Monday",
            ["Bootstrap:TradingSchedule:TradingDays:1"] = "Tuesday",
            ["Bootstrap:TradingSchedule:TradingDays:2"] = "Wednesday",
            ["Bootstrap:TradingSchedule:TradingDays:3"] = "Thursday",
            ["Bootstrap:TradingSchedule:TradingDays:4"] = "Friday",
            ["Bootstrap:TradingSchedule:WeekendBehavior"] = "ExcludeWeekends",
            ["Bootstrap:TradingSchedule:TimeZone"] = "UTC",
            ["Bootstrap:RetryPolicy:InitialDelaySeconds"] = "1",
            ["Bootstrap:RetryPolicy:MaxAutomaticRetries"] = "5",
            ["Bootstrap:RetryPolicy:Multiplier"] = "2",
            ["Bootstrap:RetryPolicy:MaxDelaySeconds"] = "60",
            ["Bootstrap:RetryPolicy:PeriodicDelayMinutes"] = "5",
            ["Bootstrap:NotificationSettings:EmailTo"] = "operator@local.test",
            ["Bootstrap:UpdatedBy"] = "bootstrap",
            ["Bootstrap:NotificationSettings:Provider"] = "RecordedOnly"
        })
        .Build();

    private static SqlPlatformConfigurationStore CreateConfigurationStore(PlatformDbContext dbContext, IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("Phase10.4.IntegrationTests");
        using var provider = services.BuildServiceProvider();
        var dataProtectionProvider = provider.GetRequiredService<IDataProtectionProvider>();
        var credentialService = new ProtectedCredentialService(dbContext, dataProtectionProvider, TimeProvider.System);
        return new SqlPlatformConfigurationStore(dbContext, configuration, credentialService, TimeProvider.System);
    }

    private static PlatformStartupInitializer CreateStartupInitializer(
        PlatformDbContext dbContext,
        SqlPlatformConfigurationStore configurationStore)
    {
        var retentionConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Retention:OperationalRecordsDays"] = "90"
            })
            .Build();
        var retentionProcessor = new OperationalRecordRetentionProcessor(
            dbContext,
            retentionConfiguration,
            TimeProvider.System,
            NullLogger<OperationalRecordRetentionProcessor>.Instance);

        return new PlatformStartupInitializer(
            dbContext,
            new PlatformConfigurationService(configurationStore),
            retentionProcessor,
            new IntegrationTestHostEnvironment(),
            NullLogger<PlatformStartupInitializer>.Instance);
    }

    private static PlatformConfigurationUpdate CreateConfigurationUpdate(
        string platformEnvironment,
        string brokerEnvironment,
        string provider,
        string emailTo,
        string? apiKey,
        string? identifier,
        string? password,
        string changedBy) => new(
            Enum.Parse<PlatformEnvironmentKind>(platformEnvironment, ignoreCase: true),
            Enum.Parse<BrokerEnvironmentKind>(brokerEnvironment, ignoreCase: true),
            new TradingScheduleConfiguration(
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
                WeekendBehavior.ExcludeWeekends,
                [],
                "UTC"),
            new RetryPolicyConfiguration(1, 5, 2, 60, 5),
            new NotificationSettingsConfiguration(provider, emailTo),
            apiKey,
            identifier,
            password,
            changedBy);

    private sealed class FailConfigurationAuditInsertInterceptor : DbCommandInterceptor
    {
        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            ThrowWhenConfigurationAuditIsInserted(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowWhenConfigurationAuditIsInserted(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ThrowWhenConfigurationAuditIsInserted(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ThrowWhenConfigurationAuditIsInserted(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static void ThrowWhenConfigurationAuditIsInserted(DbCommand command)
        {
            if (command.CommandText.Contains("ConfigurationAudits", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Injected Phase 10.4 configuration audit write failure.");
            }
        }
    }

    private sealed class IntegrationTestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = nameof(PlatformSqlServerIntegrationTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}