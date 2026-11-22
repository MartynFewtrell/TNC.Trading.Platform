using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.MarketDataRuns;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class IgDemoMarketDataRecoveryResetSqlIntegrationTests(SqlServerDatabaseFixture fixture)
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Trace: UR-02 and DO-04. Executes the saved reset asset first in its default dry-run mode and then with a count-pinned apply confirmation.
    /// Expected: dry-run changes nothing; apply clears every selected diagnostic, releases only its partial coverage, preserves budget and full-run FK history,
    /// and permits a distinct Categories-first admission. Why: recovery must be repeatable and must not erase quota, history or exclusions to manufacture readiness.
    /// </summary>
    [Fact]
    public async Task ResetScript_ShouldRearmFailedSlotAndAdmitNewCategoriesRun_WhenCountsArePinned()
    {
        var scenario = await CreateScenarioAsync();
        var server = await GetServerNameAsync();
        var script = await ReadResetScriptAsync();

        await ExecuteResetScriptAsync(script, CreateInput(scenario, server, null));
        await AssertScenarioUnchangedAsync(scenario, diagnosticsCleared: false, coverageReleased: false);

        await ExecuteResetScriptAsync(
            script,
            CreateInput(scenario, server, "Apply") with
            {
                ExpectedCycleRows = 1,
                ExpectedAttemptRows = 1,
                ExpectedCoverageRows = 1,
                ExpectedIntentRows = 0,
                ExpectedUsedRequestBudget = 7,
                ApplyConfirmation = Confirmation(scenario)
            });
        await AssertScenarioUnchangedAsync(scenario, diagnosticsCleared: true, coverageReleased: true);

        var admittedAt = DateTimeOffset.UtcNow.AddSeconds(2);
        await using var admissionContext = fixture.CreateDbContext();
        var admission = await CreateStore(admissionContext, scenario.EnvironmentId).TryAdmitAsync(
            CreateRequest(scenario.EnvironmentId, scenario.TradingDay, admittedAt, Guid.NewGuid()),
            fixture.CancellationToken);

        var newLease = Assert.IsType<MarketDataFullRunLease>(admission.Lease);
        Assert.Equal(MarketDataFullRunAdmissionStatus.Admitted, admission.Status);
        Assert.NotEqual(scenario.HistoricalRunId, newLease.RunId);
        var newRun = await admissionContext.MarketDataFullRuns.SingleAsync(
            item => item.RunId == newLease.RunId,
            fixture.CancellationToken);
        Assert.Equal("Categories", newRun.CurrentStage);
    }

    /// <summary>
    /// Trace: UR-02 and DO-04. Verifies unsupported targets, profiles, recovery scopes and modes fail before any reset mutation.
    /// Expected: mismatched server/database, a non-applied environment and broader/ambiguous modes are refused; the failed diagnostics and coverage remain.
    /// Why: a reusable maintenance script must fail closed rather than accidentally act on Live, remote, or broader-than-reviewed data.
    /// </summary>
    [Fact]
    public async Task ResetScript_ShouldRefuseAmbiguousOrNonlocalTargets_WhenInputsDoNotMatchFixture()
    {
        var scenario = await CreateScenarioAsync();
        var script = await ReadResetScriptAsync();
        var server = await GetServerNameAsync();
        var baseline = CreateInput(scenario, server, "DryRun");

        Assert.Contains(":setvar RecoveryMode \"DryRun\"", script, StringComparison.Ordinal);
        await AssertScriptRefusedAsync(script, baseline with { ExpectedServer = "NOT_THIS_SQL_SERVER" }, 51004);
        await AssertScriptRefusedAsync(script, baseline with { ExpectedDatabase = "remote-platformdb" }, 51003);
        await AssertScriptRefusedAsync(script, baseline with { EnvironmentId = scenario.LiveEnvironmentId }, 51016);
        await AssertScriptRefusedAsync(script, baseline with { RecoveryScope = "DependencyClosedCleanup" }, 51001);
        await AssertScriptRefusedAsync(script, baseline with { RecoveryMode = "Apply" }, 51008);

        await AssertScenarioUnchangedAsync(scenario, diagnosticsCleared: false, coverageReleased: false);
    }

    /// <summary>
    /// Trace: DO-04. Verifies an active lease blocks reset and stale expected counts block apply.
    /// Expected: the operation refuses both an active writer and a changed preflight snapshot without clearing diagnostics or coverage.
    /// Why: transaction locks do not replace explicit lease fencing and count rechecks.
    /// </summary>
    [Fact]
    public async Task ResetScript_ShouldRefuseActiveLeaseAndChangedCounts_WhenResetIsNoLongerSafe()
    {
        var scenario = await CreateScenarioAsync(activeCycleLease: true);
        var script = await ReadResetScriptAsync();
        var server = await GetServerNameAsync();
        await AssertScriptRefusedAsync(script, CreateInput(scenario, server, "DryRun"), 51020);

        await using (var context = fixture.CreateDbContext())
        {
            var cycle = await context.InstrumentCollectionCycleStates.SingleAsync(fixture.CancellationToken);
            cycle.LeaseOwner = null;
            cycle.LeaseExpiresAtUtc = null;
            await context.SaveChangesAsync(fixture.CancellationToken);
        }

        await AssertScriptRefusedAsync(
            script,
            CreateInput(scenario, server, "Apply") with
            {
                ExpectedCycleRows = 1,
                ExpectedAttemptRows = 99,
                ExpectedCoverageRows = 1,
                ExpectedIntentRows = 0,
                ExpectedUsedRequestBudget = 7,
                ApplyConfirmation = Confirmation(scenario)
            },
            51018);
        await AssertScenarioUnchangedAsync(scenario, diagnosticsCleared: false, coverageReleased: false);
    }

    /// <summary>
    /// Trace: DO-04. Holds the same full-run application lock used by admission while invoking the exact reset asset.
    /// Expected: the reset fails promptly on lock contention and leaves selected state unchanged.
    /// Why: independent processes must not race a recovery operation against a concurrent full-run admission.
    /// </summary>
    [Fact]
    public async Task ResetScript_ShouldRefuseConcurrentFullRunAdmission_WhenFullRunLockIsHeld()
    {
        var scenario = await CreateScenarioAsync();
        var script = await ReadResetScriptAsync();
        var server = await GetServerNameAsync();

        await using var lockConnection = new SqlConnection(fixture.ConnectionString);
        await lockConnection.OpenAsync(fixture.CancellationToken);
        await using var transaction = await lockConnection.BeginTransactionAsync(fixture.CancellationToken);
        await using (var lockCommand = lockConnection.CreateCommand())
        {
            lockCommand.Transaction = (SqlTransaction)transaction;
            lockCommand.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = N'Exclusive',
                    @LockOwner = N'Transaction', @LockTimeout = 0;
                SELECT @result;
                """;
            lockCommand.Parameters.AddWithValue(
                "@resource",
                $"MarketDataFullRuns/{scenario.EnvironmentId:N}");
            Assert.True(Convert.ToInt32(await lockCommand.ExecuteScalarAsync(fixture.CancellationToken), CultureInfo.InvariantCulture) >= 0);
        }

        await using (var contentionConnection = new SqlConnection(fixture.ConnectionString))
        {
            await contentionConnection.OpenAsync(fixture.CancellationToken);
            await using var contentionCommand = contentionConnection.CreateCommand();
            contentionCommand.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = N'Exclusive',
                    @LockOwner = N'Session', @LockTimeout = 0;
                SELECT @result;
                """;
            contentionCommand.Parameters.AddWithValue(
                "@resource",
                $"MarketDataFullRuns/{scenario.EnvironmentId:N}");
            Assert.True(Convert.ToInt32(await contentionCommand.ExecuteScalarAsync(fixture.CancellationToken), CultureInfo.InvariantCulture) < 0);
        }

        await AssertScriptRefusedAsync(
            script,
            CreateInput(scenario, server, "DryRun"),
            51012);
        await transaction.RollbackAsync(fixture.CancellationToken);
        await AssertScenarioUnchangedAsync(scenario, diagnosticsCleared: false, coverageReleased: false);
    }

    /// <summary>
    /// Trace: DO-04. Forces a SQL failure after attempt diagnostics and coverage have been changed inside the reset transaction.
    /// Expected: SQL Server rolls the complete operation back, preserving the original diagnostic values, cycle and FK-linked coverage.
    /// Why: a failed maintenance operation must not leave a half-reset slot that looks eligible but is still covered.
    /// </summary>
    [Fact]
    public async Task ResetScript_ShouldRollbackAllChanges_WhenCycleUpdateFails()
    {
        var scenario = await CreateScenarioAsync();
        var script = await ReadResetScriptAsync();
        var server = await GetServerNameAsync();
        await ExecuteFixtureSqlAsync(
            """
            CREATE TRIGGER dbo.TR_TestFailRecoveryCycleUpdate
            ON dbo.InstrumentCollectionCycleStates
            AFTER UPDATE
            AS
            BEGIN
                THROW 51070, 'Fixture-injected cycle update failure.', 1;
            END;
            """);

        try
        {
            var exception = await Assert.ThrowsAsync<SqlException>(() => ExecuteResetScriptAsync(
                script,
                CreateInput(scenario, server, "Apply") with
                {
                    ExpectedCycleRows = 1,
                    ExpectedAttemptRows = 1,
                    ExpectedCoverageRows = 1,
                    ExpectedIntentRows = 0,
                    ExpectedUsedRequestBudget = 7,
                    ApplyConfirmation = Confirmation(scenario)
                }));
            Assert.Equal(51070, exception.Number);
        }
        finally
        {
            await ExecuteFixtureSqlAsync("DROP TRIGGER IF EXISTS dbo.TR_TestFailRecoveryCycleUpdate;");
        }

        await AssertScenarioUnchangedAsync(scenario, diagnosticsCleared: false, coverageReleased: false);
    }

    private async Task<Scenario> CreateScenarioAsync(bool activeCycleLease = false)
    {
        await fixture.ResetDatabaseAsync();
        await using (var setup = fixture.CreateDbContext())
        {
            await setup.Database.MigrateAsync(fixture.CancellationToken);
        }

        var environmentId = await GetDemoEnvironmentIdAsync();
        var now = DateTimeOffset.UtcNow;
        var tradingDay = DateOnly.FromDateTime(now.UtcDateTime);
        var historicalRunId = Guid.Empty;
        var liveEnvironmentId = Guid.NewGuid();

        await using (var context = fixture.CreateDbContext())
        {
            var store = CreateStore(context, environmentId);
            var request = CreateRequest(environmentId, tradingDay, now, Guid.NewGuid());
            var result = await store.TryAdmitAsync(request, fixture.CancellationToken);
            var lease = Assert.IsType<MarketDataFullRunLease>(result.Lease);
            historicalRunId = lease.RunId;
            Assert.True(await store.CompleteAsync(
                lease,
                "Partial",
                "CategoryFailure",
                now.AddSeconds(1),
                fixture.CancellationToken));
        }

        await using (var seed = fixture.CreateDbContext())
        {
            seed.BrokerEnvironments.Add(new()
            {
                BrokerEnvironmentId = liveEnvironmentId,
                Name = "IG Live",
                NormalizedName = "IG LIVE",
                Provider = "IG",
                Kind = "Live",
                Lifecycle = "Active",
                Availability = "Available",
                EndpointProfile = "IgLive",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            var settings = await seed.InstrumentCollectionSettings.SingleOrDefaultAsync(
                item => item.BrokerEnvironmentId == environmentId,
                fixture.CancellationToken);
            if (settings is null)
            {
                settings = new InstrumentCollectionSettingsEntity
                {
                    BrokerEnvironmentId = environmentId,
                    CurrentUpdatesPerDay = 1,
                    ConfigurationVersion = 1
                };
                seed.InstrumentCollectionSettings.Add(settings);
            }

            settings.ApprovedNonTradingDailyRequestAllowance = 50;
            seed.InstrumentCollectionCycleStates.Add(new()
            {
                BrokerEnvironmentId = environmentId,
                TradingDay = tradingDay,
                ScheduledSlot = 0,
                ScheduleRevision = 1,
                CategoryPrerequisite = "Failed",
                CategoryPrerequisiteSafeError = "PrerequisiteUnavailable",
                CategoryPrerequisiteAttempts = 2,
                CategoryPrerequisiteLeaseFence = 7,
                Outcome = "Failed",
                LeaseOwner = activeCycleLease ? Guid.NewGuid() : null,
                LeaseFence = 4,
                LeaseExpiresAtUtc = activeCycleLease ? now.AddMinutes(1) : now.AddMinutes(-1),
                UsedRequestBudget = 7
            });
            seed.InstrumentCollectionCategoryAttempts.Add(new()
            {
                BrokerEnvironmentId = environmentId,
                TradingDay = tradingDay,
                ScheduledSlot = 0,
                CategoryCode = "CAT-A",
                Attempts = 3,
                State = "Failed",
                LeaseFence = 8,
                SafeError = "IncompleteCollection",
                FailureKind = "IncompleteCollection",
                ProviderErrorCode = "error.public-api.exceeded-application-allowance",
                HttpStatus = 403,
                Operation = "instruments",
                PageNumber = 0,
                Expected = 25,
                Actual = 17,
                UpdatedAtUtc = now
            });
            seed.IgProviderRateReservations.Add(new()
            {
                RequestId = Guid.NewGuid(),
                ScopeType = "App",
                ScopeHash = new string('a', 64),
                ReservedAtUtc = now
            });
            seed.MarketDetailEligibility.Add(new()
            {
                BrokerEnvironmentId = environmentId,
                Epic = "CS.D.RECOVERYTEST.CFD.IP",
                Status = "Excluded",
                EvidenceCode = "ProviderConfirmedUnavailable",
                ExcludedAtUtc = now,
                UpdatedAtUtc = now
            });
            await seed.SaveChangesAsync(fixture.CancellationToken);
        }

        return new(environmentId, tradingDay, 0, historicalRunId, liveEnvironmentId);
    }

    private async Task AssertScenarioUnchangedAsync(
        Scenario scenario,
        bool diagnosticsCleared,
        bool coverageReleased)
    {
        await using var context = fixture.CreateDbContext();
        var cycle = await context.InstrumentCollectionCycleStates.SingleAsync(fixture.CancellationToken);
        var attempt = await context.InstrumentCollectionCategoryAttempts.SingleAsync(fixture.CancellationToken);
        var historicalRun = await context.MarketDataFullRuns.SingleAsync(
            item => item.RunId == scenario.HistoricalRunId,
            fixture.CancellationToken);
        var historicalCategories = await context.MarketDataFullRunCategories.CountAsync(
            item => item.RunId == scenario.HistoricalRunId,
            fixture.CancellationToken);
        var reservationCount = await context.IgProviderRateReservations.CountAsync(fixture.CancellationToken);
        var exclusionCount = await context.MarketDetailEligibility.CountAsync(
            item => item.BrokerEnvironmentId == scenario.EnvironmentId && item.Status == "Excluded",
            fixture.CancellationToken);
        var settings = await context.InstrumentCollectionSettings.SingleAsync(
            item => item.BrokerEnvironmentId == scenario.EnvironmentId,
            fixture.CancellationToken);
        var dailyBudget = await context.InstrumentCollectionCycleStates
            .Where(item => item.BrokerEnvironmentId == scenario.EnvironmentId && item.TradingDay == scenario.TradingDay)
            .SumAsync(item => item.UsedRequestBudget, fixture.CancellationToken);
        var coverageCount = await context.MarketDataFullRunSlotCoverages.CountAsync(
            item => item.BrokerEnvironmentId == scenario.EnvironmentId
                && item.TradingDay == scenario.TradingDay
                && item.ScheduledSlot == scenario.Slot,
            fixture.CancellationToken);

        Assert.Equal(diagnosticsCleared ? "Pending" : "Failed", cycle.Outcome);
        Assert.Equal(diagnosticsCleared ? "Pending" : "Failed", cycle.CategoryPrerequisite);
        Assert.Equal(7, cycle.UsedRequestBudget);
        Assert.Equal(4, cycle.LeaseFence);
        Assert.Equal(7, cycle.CategoryPrerequisiteLeaseFence);
        Assert.Equal(diagnosticsCleared ? "Pending" : "Failed", attempt.State);
        Assert.Equal(diagnosticsCleared ? 0 : 3, attempt.Attempts);
        Assert.Equal(diagnosticsCleared ? null : "IncompleteCollection", attempt.FailureKind);
        Assert.Equal(diagnosticsCleared ? null : "error.public-api.exceeded-application-allowance", attempt.ProviderErrorCode);
        Assert.Equal(diagnosticsCleared ? null : 403, attempt.HttpStatus);
        Assert.Equal(diagnosticsCleared ? null : "instruments", attempt.Operation);
        Assert.Equal(diagnosticsCleared ? null : 0, attempt.PageNumber);
        Assert.Equal(diagnosticsCleared ? null : 25, attempt.Expected);
        Assert.Equal(diagnosticsCleared ? null : 17, attempt.Actual);
        Assert.Equal(diagnosticsCleared ? null : "IncompleteCollection", attempt.SafeError);
        Assert.Equal(coverageReleased ? 0 : 1, coverageCount);
        Assert.Equal("Partial", historicalRun.Outcome);
        Assert.Equal(2, historicalCategories);
        Assert.Equal(1, reservationCount);
        Assert.Equal(1, exclusionCount);
        Assert.Equal(50, settings.ApprovedNonTradingDailyRequestAllowance);
        Assert.Equal(7, dailyBudget);
    }

    private async Task AssertScriptRefusedAsync(string script, ResetInputs input, int expectedNumber)
    {
        var exception = await Assert.ThrowsAsync<SqlException>(() => ExecuteResetScriptAsync(script, input));
        Assert.Equal(expectedNumber, exception.Number);
    }

    private async Task ExecuteResetScriptAsync(string script, ResetInputs input)
    {
        var inputValues = Regex.Matches(script, @"(?m)^:setvar\s+(\w+)\s+""([^""]*)""\s*$")
            .ToDictionary(
                match => match.Groups[1].Value,
                match => match.Groups[2].Value,
                StringComparer.Ordinal);
        if (input.RecoveryMode is not null)
        {
            inputValues["RecoveryMode"] = input.RecoveryMode;
        }

        var invocationValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RecoveryScope"] = input.RecoveryScope,
            ["ExpectedServer"] = input.ExpectedServer,
            ["ExpectedDatabase"] = input.ExpectedDatabase,
            ["EnvironmentId"] = input.EnvironmentId.ToString("D"),
            ["EndpointProfile"] = input.EndpointProfile,
            ["TradingDay"] = input.TradingDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["ScheduledSlot"] = input.Slot.ToString(CultureInfo.InvariantCulture),
            ["ExpectedCycleRows"] = input.ExpectedCycleRows.ToString(CultureInfo.InvariantCulture),
            ["ExpectedAttemptRows"] = input.ExpectedAttemptRows.ToString(CultureInfo.InvariantCulture),
            ["ExpectedCoverageRows"] = input.ExpectedCoverageRows.ToString(CultureInfo.InvariantCulture),
            ["ExpectedIntentRows"] = input.ExpectedIntentRows.ToString(CultureInfo.InvariantCulture),
            ["ExpectedUsedRequestBudget"] = input.ExpectedUsedRequestBudget.ToString(CultureInfo.InvariantCulture),
            ["ApplyConfirmation"] = input.ApplyConfirmation
        };

        var executableSql = Regex.Replace(script, @"(?m)^:setvar[^\r\n]*(?:\r?\n|$)", string.Empty);
        foreach (var (name, value) in invocationValues)
        {
            inputValues[name] = value;
        }

        foreach (var (name, value) in inputValues)
        {
            executableSql = executableSql.Replace(
                $"$({name})",
                value.Replace("'", "''", StringComparison.Ordinal),
                StringComparison.Ordinal);
        }

        Assert.DoesNotContain("$(", executableSql, StringComparison.Ordinal);
        await ExecuteFixtureSqlAsync(executableSql);
    }

    private async Task ExecuteFixtureSqlAsync(string sql)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(fixture.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = (int)CommandTimeout.TotalSeconds;
        await command.ExecuteNonQueryAsync(fixture.CancellationToken);
    }

    private async Task<string> ReadResetScriptAsync()
    {
        var fileName = Path.Combine("infra", "scripts", "reset-ig-demo-market-data-recovery.sql");
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, fileName);
                if (File.Exists(candidate))
                {
                    return await File.ReadAllTextAsync(candidate, fixture.CancellationToken);
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"Could not locate the checked-in reset SQL asset '{fileName}'.");
    }

    private async Task<string> GetServerNameAsync()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(fixture.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CONVERT(nvarchar(128), @@SERVERNAME)";
        return (string)(await command.ExecuteScalarAsync(fixture.CancellationToken)
            ?? throw new InvalidOperationException("Fixture SQL Server did not report @@SERVERNAME."));
    }

    private async Task<Guid> GetDemoEnvironmentIdAsync()
    {
        await using var context = fixture.CreateDbContext();
        return await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(context, fixture.CancellationToken);
    }

    private static EfMarketDataFullRunStore CreateStore(PlatformDbContext context, Guid environmentId) =>
        new(context, new FixedEnvironmentResolver(environmentId));

    private static MarketDataFullRunAdmissionRequest CreateRequest(
        Guid environmentId,
        DateOnly tradingDay,
        DateTimeOffset admittedAt,
        Guid owner) =>
        new(
            BrokerEnvironmentKind.Demo,
            environmentId,
            "IgDemo",
            tradingDay,
            admittedAt,
            admittedAt.AddHours(4),
            1,
            1,
            1,
            0,
            MarketDataFullRunTrigger.Scheduled,
            ["CAT-A", "CAT-B"],
            [new(tradingDay, 1, 0)],
            owner,
            TimeSpan.FromMinutes(1));

    private ResetInputs CreateInput(Scenario scenario, string server, string? mode) =>
        new(
            mode,
            "FailedSlotRearm",
            server,
            new SqlConnectionStringBuilder(fixture.ConnectionString).InitialCatalog,
            scenario.EnvironmentId,
            "IgDemo",
            scenario.TradingDay,
            scenario.Slot,
            -1,
            -1,
            -1,
            -1,
            -1,
            string.Empty);

    private string Confirmation(Scenario scenario) =>
        $"APPLY-FAILED-SLOT|{new SqlConnectionStringBuilder(fixture.ConnectionString).InitialCatalog}|{scenario.EnvironmentId:D}|{scenario.TradingDay:yyyy-MM-dd}|{scenario.Slot}";

    private sealed record Scenario(Guid EnvironmentId, DateOnly TradingDay, int Slot, Guid HistoricalRunId, Guid LiveEnvironmentId);

    private sealed record ResetInputs(
        string? RecoveryMode,
        string RecoveryScope,
        string ExpectedServer,
        string ExpectedDatabase,
        Guid EnvironmentId,
        string EndpointProfile,
        DateOnly TradingDay,
        int Slot,
        int ExpectedCycleRows,
        int ExpectedAttemptRows,
        int ExpectedCoverageRows,
        int ExpectedIntentRows,
        int ExpectedUsedRequestBudget,
        string ApplyConfirmation);

    private sealed class FixedEnvironmentResolver(Guid environmentId) : IAppliedBrokerEnvironmentContextResolver
    {
        public Task<AppliedBrokerEnvironmentContext?> ResolveAppliedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(
                new(environmentId, "IG", "Demo", "Active", "Available", "IgDemo", true));

        public Task<AppliedBrokerEnvironmentContext?> ResolveAsync(
            Guid requestedBrokerEnvironmentId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AppliedBrokerEnvironmentContext?>(
                requestedBrokerEnvironmentId == environmentId
                    ? new(environmentId, "IG", "Demo", "Active", "Available", "IgDemo", true)
                    : null);
    }
}
