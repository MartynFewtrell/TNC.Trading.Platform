using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.Application.UnitTests;

public class AuthRetryCycleTests
{
    [Fact]
    public async Task UpsertRetryCycleAsync_WithExistingCycle_UpdatesRatherThanDuplicates()
    {
        using var dbContext = ApplicationReflection.CreateDbContext();
        var configuration = new ConfigurationBuilder().Build();
        var protectedCredentialService = ApplicationReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Platform.ProtectedCredentialService",
            dbContext,
            ApplicationReflection.CreateDataProtectionProvider(),
            TimeProvider.System);
        var configurationStore = ApplicationReflection.Create(
            "TNC.Trading.Platform.Infrastructure.Platform.SqlPlatformConfigurationStore",
            dbContext,
            configuration,
            protectedCredentialService,
            TimeProvider.System);
        var configurationService = ApplicationReflection.Create(
            "TNC.Trading.Platform.Application.Services.PlatformConfigurationService",
            configurationStore);
        var notificationDispatcher = CreateNotificationDispatcher(dbContext);
        var coordinatorType = ApplicationReflection.GetType("TNC.Trading.Platform.Application.Services.PlatformStateCoordinator");
        var coordinator = Activator.CreateInstance(
            coordinatorType,
            configuration,
            configurationService,
            ApplicationReflection.Create("TNC.Trading.Platform.Infrastructure.Platform.EfPlatformRuntimeStateStore", dbContext),
            ApplicationReflection.Create("TNC.Trading.Platform.Infrastructure.Platform.EfPlatformRetryCycleStore", dbContext),
            ApplicationReflection.Create("TNC.Trading.Platform.Infrastructure.Platform.EfPlatformEventStore", dbContext),
            notificationDispatcher,
            ApplicationReflection.Create("TNC.Trading.Platform.Application.Services.TradingScheduleGate"),
            TimeProvider.System,
            ApplicationReflection.CreateNullLogger(coordinatorType))!;

        var configurationSnapshot = CreateConfigurationSnapshot();
        var state = ApplicationReflection.Create("TNC.Trading.Platform.Application.Configuration.PlatformRuntimeState");
        var retryCycleId = Guid.NewGuid();
        var nextRetryAtUtc = DateTimeOffset.UtcNow.AddSeconds(1);

        ApplicationReflection.SetProperty(state, "RetryPhase", ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.AuthRetryPhase", "InitialAutomatic"));
        ApplicationReflection.SetProperty(state, "AutomaticAttemptNumber", 1);
        ApplicationReflection.SetProperty(state, "NextRetryAtUtc", nextRetryAtUtc);
        ApplicationReflection.SetProperty(state, "RetryLimitReached", false);

        _ = await ApplicationReflection.InvokeAsync(coordinator, "UpsertRetryCycleAsync", retryCycleId, configurationSnapshot, state, "Automatic", false, 1, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        ApplicationReflection.SetProperty(state, "RetryPhase", ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.AuthRetryPhase", "Periodic"));
        ApplicationReflection.SetProperty(state, "AutomaticAttemptNumber", 5);
        ApplicationReflection.SetProperty(state, "NextRetryAtUtc", nextRetryAtUtc.AddMinutes(5));
        ApplicationReflection.SetProperty(state, "RetryLimitReached", true);

        _ = await ApplicationReflection.InvokeAsync(coordinator, "UpsertRetryCycleAsync", retryCycleId, configurationSnapshot, state, "Automatic", true, 60, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var cycles = ((IEnumerable<object>)dbContext.GetType().GetProperty("AuthRetryCycles", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();

        var cycle = Assert.Single(cycles);
        Assert.Equal("Periodic", ApplicationReflection.GetProperty<string>(cycle, "RetryPhase"));
        Assert.Equal(5, ApplicationReflection.GetProperty<int>(cycle, "AutomaticAttemptNumber"));
        Assert.True(ApplicationReflection.GetProperty<bool>(cycle, "RetryLimitReached"));
        Assert.True(ApplicationReflection.GetProperty<bool>(cycle, "FailureNotificationSent"));
        Assert.Equal(60, ApplicationReflection.GetProperty<int?>(cycle, "LastDelaySeconds"));
    }

    private static object CreateNotificationDispatcher(DbContext dbContext)
    {
        var providerType = ApplicationReflection.GetType("TNC.Trading.Platform.Infrastructure.Notifications.INotificationProvider");
        var dispatcherType = ApplicationReflection.GetType("TNC.Trading.Platform.Infrastructure.Platform.NotificationDispatcher");
        var emptyProviders = Array.CreateInstance(providerType, 0);

        return Activator.CreateInstance(
            dispatcherType,
            dbContext,
            emptyProviders,
            ApplicationReflection.CreateNullLogger(dispatcherType),
            TimeProvider.System)!;
    }

    private static object CreateConfigurationSnapshot()
    {
        return ApplicationReflection.Create(
            "TNC.Trading.Platform.Application.Configuration.PlatformConfigurationSnapshot",
            ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.PlatformEnvironmentKind", "Live"),
            ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.BrokerEnvironmentKind", "Demo"),
            ApplicationReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.TradingScheduleConfiguration",
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                ApplicationReflection.ParseEnum("TNC.Trading.Platform.Application.Configuration.WeekendBehavior", "ExcludeWeekends"),
                Array.Empty<DateOnly>(),
                "UTC"),
            ApplicationReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.RetryPolicyConfiguration",
                1,
                5,
                2,
                60,
                5),
            ApplicationReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.NotificationSettingsConfiguration",
                "RecordedOnly",
                "owner@example.com"),
            ApplicationReflection.Create(
                "TNC.Trading.Platform.Application.Configuration.CredentialPresence",
                true,
                true,
                true),
            true,
            true,
            DateTimeOffset.UtcNow,
            false);
    }
}
