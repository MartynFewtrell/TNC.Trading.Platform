using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.BrokerEnvironments;
using TNC.Trading.Platform.Infrastructure.Credentials.DataProtection;
using TNC.Trading.Platform.Infrastructure.Configuration.SqlServer;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework;
using TNC.Trading.Platform.Infrastructure.Persistence.EntityFramework.Entities;
using TNC.Trading.Platform.Infrastructure.Startup;

namespace TNC.Trading.Platform.Infrastructure.IntegrationTests;

[Collection("SQL Server")]
public sealed class AppliedBrokerScheduleProfileSqlServerTests(SqlServerDatabaseFixture fixture)
{
    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1 new-profile defaults.
    /// Verifies broker creation stores the agreed London weekday schedule while continuing to create the existing retry and notification profiles.
    /// Expected: a new broker schedule has 08:00–17:00 hours, Monday–Friday eligibility, excluded weekends, and Europe/London.
    /// Why: new environments need a safe predictable Trading Day without mutating established broker configuration.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldUseTradingDayDefaults_WhenNewBrokerProfileIsCreated()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        await dbContext.Database.MigrateAsync();
        var services = new ServiceCollection();
        services.AddDataProtection();
        using var serviceProvider = services.BuildServiceProvider();
        var credentials = new ProtectedCredentialService(
            dbContext,
            serviceProvider.GetRequiredService<IDataProtectionProvider>(),
            TimeProvider.System);
        var catalog = new SqlBrokerEnvironmentCatalogService(
            dbContext,
            credentials,
            new PlatformEnvironmentContext(PlatformEnvironmentKind.Test),
            TimeProvider.System);

        var result = await catalog.CreateAsync(
            new CreateBrokerEnvironmentCommand(
                "New Demo",
                "New Demo",
                "Ig",
                "Demo",
                "IgDemo",
                "integration-test"),
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        var brokerId = Assert.IsType<BrokerEnvironmentCatalogItem>(result.Item).Id;
        var schedule = await dbContext.BrokerEnvironmentScheduleProfiles.AsNoTracking()
            .SingleAsync(item => item.BrokerEnvironmentId == brokerId);

        Assert.Equal(new TimeOnly(8, 0), schedule.TradingHoursStart);
        Assert.Equal(new TimeOnly(17, 0), schedule.TradingHoursEnd);
        Assert.Equal("Monday,Tuesday,Wednesday,Thursday,Friday", schedule.TradingDaysCsv);
        Assert.Equal("ExcludeWeekends", schedule.WeekendBehavior);
        Assert.Equal("[]", schedule.BankHolidayExclusionsJson);
        Assert.Equal("Europe/London", schedule.TimeZone);
        Assert.True(await dbContext.BrokerEnvironmentRetryProfiles.AnyAsync(item => item.BrokerEnvironmentId == brokerId));
        Assert.True(await dbContext.BrokerEnvironmentNotificationProfiles.AnyAsync(item => item.BrokerEnvironmentId == brokerId));
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1 applied-versus-selected schedule contract.
    /// Verifies the SQL adapter reads and writes only the applied broker's schedule even when another broker is selected.
    /// Expected: the applied profile changes, the selected profile remains unchanged, and retry/notification profiles are untouched.
    /// Why: applying a broker is restart-fenced, so editing the pending selection must not redirect the live Trading Day schedule or alter adjacent configuration.
    /// </summary>
    [Fact]
    public async Task SaveAppliedAsync_ShouldUpdateOnlyAppliedSchedule_WhenAnotherBrokerIsSelected()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        await dbContext.Database.MigrateAsync();

        var demoId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(dbContext, CancellationToken.None);
        var selectedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var now = DateTimeOffset.UtcNow;
        var originalSelectedSchedule = new BrokerEnvironmentScheduleProfileEntity
        {
            BrokerEnvironmentId = selectedId,
            DefaultsVersion = 1,
            TradingHoursStart = new TimeOnly(9, 0),
            TradingHoursEnd = new TimeOnly(15, 0),
            TradingDaysCsv = "Monday,Tuesday,Wednesday,Thursday,Friday",
            WeekendBehavior = "ExcludeWeekends",
            BankHolidayExclusionsJson = "[]",
            TimeZone = "UTC"
        };
        dbContext.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = selectedId,
            Name = "IG Demo Next",
            NormalizedName = "IG DEMO NEXT",
            Provider = "Ig",
            Kind = "Demo",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "IgDemo",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.BrokerEnvironmentScheduleProfiles.Add(originalSelectedSchedule);
        var selection = await dbContext.BrokerEnvironmentSelections.SingleAsync();
        selection.SelectedBrokerEnvironmentId = selectedId;
        selection.RestartRequired = true;

        var retryProfile = await dbContext.BrokerEnvironmentRetryProfiles.SingleAsync(item => item.BrokerEnvironmentId == demoId);
        retryProfile.InitialDelaySeconds = 13;
        var notificationProfile = await dbContext.BrokerEnvironmentNotificationProfiles.SingleAsync(item => item.BrokerEnvironmentId == demoId);
        notificationProfile.Enabled = true;
        await dbContext.SaveChangesAsync();

        var store = new SqlAppliedBrokerScheduleProfileStore(
            dbContext,
            new PlatformEnvironmentContext(PlatformEnvironmentKind.Test),
            TimeProvider.System);
        var before = await store.GetAppliedAsync(CancellationToken.None);
        Assert.Equal(demoId, before.BrokerEnvironmentId);

        var requestedSchedule = new TradingScheduleConfiguration(
            new TimeOnly(8, 15),
            new TimeOnly(17, 0),
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            WeekendBehavior.ExcludeWeekends,
            [new DateOnly(2026, 12, 25)],
            "Europe/London");
        var saved = await store.SaveAppliedAsync(requestedSchedule, "integration-test", CancellationToken.None);

        Assert.Equal(demoId, saved.BrokerEnvironmentId);
        Assert.Equal(requestedSchedule.StartOfDay, saved.TradingSchedule.StartOfDay);
        Assert.Equal(requestedSchedule.EndOfDay, saved.TradingSchedule.EndOfDay);
        Assert.Equal(requestedSchedule.TradingDays, saved.TradingSchedule.TradingDays);
        Assert.Equal(requestedSchedule.BankHolidayExclusions, saved.TradingSchedule.BankHolidayExclusions);
        Assert.Equal(requestedSchedule.TimeZone, saved.TradingSchedule.TimeZone);
        Assert.Equal(2, saved.ScheduleVersion);
        dbContext.ChangeTracker.Clear();
        var appliedSchedule = await dbContext.BrokerEnvironmentScheduleProfiles.AsNoTracking()
            .SingleAsync(item => item.BrokerEnvironmentId == demoId);
        var persistedSelectedSchedule = await dbContext.BrokerEnvironmentScheduleProfiles.AsNoTracking()
            .SingleAsync(item => item.BrokerEnvironmentId == selectedId);
        var persistedRetry = await dbContext.BrokerEnvironmentRetryProfiles.AsNoTracking()
            .SingleAsync(item => item.BrokerEnvironmentId == demoId);
        var persistedNotifications = await dbContext.BrokerEnvironmentNotificationProfiles.AsNoTracking()
            .SingleAsync(item => item.BrokerEnvironmentId == demoId);

        Assert.Equal(new TimeOnly(8, 15), appliedSchedule.TradingHoursStart);
        Assert.Equal(new TimeOnly(17, 0), appliedSchedule.TradingHoursEnd);
        Assert.Equal("Europe/London", appliedSchedule.TimeZone);
        Assert.Equal(2, appliedSchedule.ScheduleVersion);
        Assert.Equal(1, appliedSchedule.DefaultsVersion);
        Assert.Equal(originalSelectedSchedule.TradingHoursStart, persistedSelectedSchedule.TradingHoursStart);
        Assert.Equal(originalSelectedSchedule.TimeZone, persistedSelectedSchedule.TimeZone);
        Assert.Equal(13, persistedRetry.InitialDelaySeconds);
        Assert.True(persistedNotifications.Enabled);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, one-time legacy schedule reconciliation.
    /// Verifies a divergent legacy schedule blocks use until an Operator saves the applied profile and that the audit record ends later comparisons.
    /// Expected: the initial read reports reconciliation required, the explicit save records the acting Operator, and later legacy changes do not reopen the gate.
    /// Why: the legacy platform schedule must be reconciled once without remaining a second runtime authority.
    /// </summary>
    [Fact]
    public async Task SaveAppliedAsync_ShouldRecordOneTimeReconciliation_WhenLegacyScheduleDiffers()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        await dbContext.Database.MigrateAsync();
        var demoId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(dbContext, CancellationToken.None);
        var platformConfiguration = new PlatformConfigurationEntity
        {
            PlatformEnvironment = "Test",
            BrokerEnvironment = "Demo",
            TradingHoursStart = new TimeOnly(8, 0),
            TradingHoursEnd = new TimeOnly(16, 30),
            TradingDaysCsv = "Monday,Tuesday,Wednesday,Thursday,Friday",
            WeekendBehavior = "ExcludeWeekends",
            BankHolidayExclusionsJson = "[]",
            TimeZone = "UTC",
            RetryInitialDelaySeconds = 1,
            RetryMaxAutomaticRetries = 5,
            RetryMultiplier = 2,
            RetryMaxDelaySeconds = 60,
            RetryPeriodicDelayMinutes = 5,
            NotificationProvider = "RecordedOnly",
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedBy = "migration"
        };
        dbContext.PlatformConfigurations.Add(platformConfiguration);
        await dbContext.SaveChangesAsync();
        dbContext.ConfigurationAudits.Add(new ConfigurationAuditEntity
        {
            ConfigurationId = platformConfiguration.ConfigurationId,
            PlatformEnvironment = "Test",
            BrokerEnvironment = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ChangedBy = "another-environment-operator",
            ChangeType = "AppliedBrokerScheduleLegacyReconciled",
            Summary = "A different broker schedule was reconciled.",
            DetailsJson = "{}",
            CorrelationId = Guid.NewGuid().ToString("N")
        });
        await dbContext.SaveChangesAsync();

        var store = new SqlAppliedBrokerScheduleProfileStore(
            dbContext,
            new PlatformEnvironmentContext(PlatformEnvironmentKind.Test),
            TimeProvider.System);
        var before = await store.GetAppliedAsync(CancellationToken.None);

        Assert.Equal(demoId, before.BrokerEnvironmentId);
        Assert.True(before.LegacyReconciliationRequired);
        Assert.Single(await dbContext.ConfigurationAudits.AsNoTracking().ToListAsync());

        var saved = await store.SaveAppliedAsync(before.TradingSchedule, "schedule-operator", CancellationToken.None);
        var reconciliationAudit = await dbContext.ConfigurationAudits.AsNoTracking()
            .SingleAsync(item => item.ChangeType == "AppliedBrokerScheduleLegacyReconciled"
                && item.BrokerEnvironment == demoId.ToString("N"));

        Assert.False(saved.LegacyReconciliationRequired);
        Assert.Equal("schedule-operator", reconciliationAudit.ChangedBy);
        Assert.Contains(demoId.ToString(), reconciliationAudit.DetailsJson, StringComparison.OrdinalIgnoreCase);

        platformConfiguration.TradingHoursEnd = new TimeOnly(15, 0);
        await dbContext.SaveChangesAsync();
        var afterLegacyChange = await store.GetAppliedAsync(CancellationToken.None);

        Assert.False(afterLegacyChange.LegacyReconciliationRequired);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, shared runtime Trading Day schedule.
    /// Verifies runtime configuration resolves the applied broker profile after a different broker has been selected but not applied.
    /// Expected: runtime exposes the updated applied profile schedule, not the legacy platform schedule or pending broker schedule.
    /// Why: admission and related market-data runtime consumers must evaluate the same schedule operators edit in Configuration.
    /// </summary>
    [Fact]
    public async Task GetRuntimeAsync_ShouldUseAppliedProfileSchedule_WhenAnotherBrokerIsSelected()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        await dbContext.Database.MigrateAsync();
        var demoId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(dbContext, CancellationToken.None);
        var selectedId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var now = DateTimeOffset.UtcNow;
        dbContext.BrokerEnvironments.Add(new BrokerEnvironmentEntity
        {
            BrokerEnvironmentId = selectedId,
            Name = "IG Demo Pending",
            NormalizedName = "IG DEMO PENDING",
            Provider = "Ig",
            Kind = "Demo",
            Lifecycle = "Active",
            Availability = "Available",
            EndpointProfile = "IgDemo",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.BrokerEnvironmentScheduleProfiles.Add(new BrokerEnvironmentScheduleProfileEntity
        {
            BrokerEnvironmentId = selectedId,
            DefaultsVersion = 1,
            TradingHoursStart = new TimeOnly(9, 0),
            TradingHoursEnd = new TimeOnly(15, 0),
            TradingDaysCsv = "Monday,Tuesday,Wednesday,Thursday,Friday",
            WeekendBehavior = "ExcludeWeekends",
            BankHolidayExclusionsJson = "[]",
            TimeZone = "UTC"
        });
        var selection = await dbContext.BrokerEnvironmentSelections.SingleAsync();
        selection.SelectedBrokerEnvironmentId = selectedId;
        selection.RestartRequired = true;
        dbContext.PlatformConfigurations.Add(new PlatformConfigurationEntity
        {
            PlatformEnvironment = "Test",
            BrokerEnvironment = "Demo",
            TradingHoursStart = new TimeOnly(8, 0),
            TradingHoursEnd = new TimeOnly(16, 0),
            TradingDaysCsv = "Monday,Tuesday,Wednesday,Thursday,Friday",
            WeekendBehavior = "ExcludeWeekends",
            BankHolidayExclusionsJson = "[]",
            TimeZone = "Europe/London",
            RetryInitialDelaySeconds = 1,
            RetryMaxAutomaticRetries = 5,
            RetryMultiplier = 2,
            RetryMaxDelaySeconds = 60,
            RetryPeriodicDelayMinutes = 5,
            NotificationProvider = "RecordedOnly",
            UpdatedAtUtc = now,
            UpdatedBy = "integration-test"
        });
        await dbContext.SaveChangesAsync();

        var platformEnvironmentContext = new PlatformEnvironmentContext(PlatformEnvironmentKind.Test);
        var appliedScheduleStore = new SqlAppliedBrokerScheduleProfileStore(
            dbContext,
            platformEnvironmentContext,
            TimeProvider.System);
        var initialProfile = await appliedScheduleStore.GetAppliedAsync(CancellationToken.None);
        Assert.True(initialProfile.LegacyReconciliationRequired);

        var requestedSchedule = new TradingScheduleConfiguration(
            new TimeOnly(8, 30),
            new TimeOnly(16, 30),
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            WeekendBehavior.ExcludeWeekends,
            [],
            "Europe/London");
        var services = new ServiceCollection();
        services.AddDataProtection();
        using (var serviceProvider = services.BuildServiceProvider())
        {
            var credentialService = new ProtectedCredentialService(
                dbContext,
                serviceProvider.GetRequiredService<IDataProtectionProvider>(),
                TimeProvider.System);
            var configurationStore = new SqlPlatformConfigurationStore(
                dbContext,
                new ConfigurationBuilder().Build(),
                credentialService,
                TimeProvider.System,
                platformEnvironmentContext,
                appliedScheduleStore);

            var beforeReconciliation = await configurationStore.GetRuntimeAsync(null, null, CancellationToken.None);
            Assert.True(beforeReconciliation.MarketDataScheduleReconciliationRequired);

            await appliedScheduleStore.SaveAppliedAsync(requestedSchedule, "integration-test", CancellationToken.None);
            var runtime = await configurationStore.GetRuntimeAsync(null, null, CancellationToken.None);

            Assert.Equal(demoId, initialProfile.BrokerEnvironmentId);
            Assert.False(runtime.MarketDataScheduleReconciliationRequired);
            Assert.Equal(requestedSchedule.StartOfDay, runtime.TradingSchedule.StartOfDay);
            Assert.Equal(requestedSchedule.EndOfDay, runtime.TradingSchedule.EndOfDay);
            Assert.Equal(requestedSchedule.TradingDays, runtime.TradingSchedule.TradingDays);
            Assert.Equal(requestedSchedule.TimeZone, runtime.TradingSchedule.TimeZone);

            var persistedAppliedProfile = await dbContext.BrokerEnvironmentScheduleProfiles
                .SingleAsync(item => item.BrokerEnvironmentId == demoId);
            persistedAppliedProfile.TimeZone = "Not/A-Time-Zone";
            await dbContext.SaveChangesAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => configurationStore.GetRuntimeAsync(null, null, CancellationToken.None));

            selection.AppliedBrokerEnvironmentId = null;
            await dbContext.SaveChangesAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => configurationStore.GetRuntimeAsync(null, null, CancellationToken.None));
        }
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1 schedule bootstrap decisions.
    /// Verifies startup leaves an existing broker schedule untouched and supplies current defaults only when creating a missing profile.
    /// Expected: the customized profile remains intact across startup; a subsequent missing-profile recovery uses 08:00–17:00 Europe/London weekdays.
    /// Why: operators' established settings must not be silently migrated or overwritten, while newly created profiles need the agreed safe default.
    /// </summary>
    [Fact]
    public async Task EnsureRequiredCatalogAsync_ShouldPreserveExistingProfileAndDefaultNewProfile_WhenStartupRuns()
    {
        await fixture.ResetDatabaseAsync();
        await using var dbContext = fixture.CreateDbContext();
        await dbContext.Database.MigrateAsync();
        var demoId = await SqlServerDatabaseFixture.GetIgDemoBrokerEnvironmentIdAsync(dbContext, CancellationToken.None);
        var profile = await dbContext.BrokerEnvironmentScheduleProfiles.SingleAsync(item => item.BrokerEnvironmentId == demoId);
        Assert.Equal(new TimeOnly(8, 0), profile.TradingHoursStart);
        Assert.Equal(new TimeOnly(17, 0), profile.TradingHoursEnd);
        Assert.Equal("Europe/London", profile.TimeZone);
        profile.TradingHoursStart = new TimeOnly(7, 30);
        profile.TradingHoursEnd = new TimeOnly(16, 30);
        profile.TimeZone = "UTC";
        await dbContext.SaveChangesAsync();

        var service = new BrokerEnvironmentCatalogIntegrityService(
            dbContext,
            TimeProvider.System,
            NullLogger<BrokerEnvironmentCatalogIntegrityService>.Instance);
        await service.EnsureRequiredCatalogAsync(CancellationToken.None);

        Assert.Equal(new TimeOnly(7, 30), profile.TradingHoursStart);
        Assert.Equal(new TimeOnly(16, 30), profile.TradingHoursEnd);
        Assert.Equal("UTC", profile.TimeZone);

        dbContext.BrokerEnvironmentScheduleProfiles.Remove(profile);
        await dbContext.SaveChangesAsync();
        await service.EnsureRequiredCatalogAsync(CancellationToken.None);
        var restored = await dbContext.BrokerEnvironmentScheduleProfiles.AsNoTracking()
            .SingleAsync(item => item.BrokerEnvironmentId == demoId);

        Assert.Equal(new TimeOnly(8, 0), restored.TradingHoursStart);
        Assert.Equal(new TimeOnly(17, 0), restored.TradingHoursEnd);
        Assert.Equal("Monday,Tuesday,Wednesday,Thursday,Friday", restored.TradingDaysCsv);
        Assert.Equal("ExcludeWeekends", restored.WeekendBehavior);
        Assert.Equal("Europe/London", restored.TimeZone);
        Assert.Equal("[]", restored.BankHolidayExclusionsJson);
    }
}
