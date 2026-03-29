using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.Api.UnitTests;

public class AuthRetryCycleTests
{
    [Fact]
    public async Task UpsertRetryCycleAsync_WithExistingCycle_UpdatesRatherThanDuplicates()
    {
        using var dbContext = ApiReflection.CreateDbContext();
        var configuration = new ConfigurationBuilder().Build();
        var protectedCredentialService = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Infrastructure.Platform.ProtectedCredentialService",
            dbContext,
            ApiReflection.CreateDataProtectionProvider(),
            TimeProvider.System);
        var configurationService = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Infrastructure.Platform.PlatformConfigurationService",
            dbContext,
            configuration,
            protectedCredentialService,
            TimeProvider.System);
        var notificationDispatcher = CreateNotificationDispatcher(dbContext);
        var coordinatorType = ApiReflection.GetType("TNC.Trading.Platform.Api.Infrastructure.Platform.PlatformStateCoordinator");
        var coordinator = Activator.CreateInstance(
            coordinatorType,
            dbContext,
            configuration,
            configurationService,
            ApiReflection.Create("TNC.Trading.Platform.Api.Infrastructure.Platform.TradingScheduleGate"),
            notificationDispatcher,
            TimeProvider.System,
            ApiReflection.CreateNullLogger(coordinatorType))!;

        var configurationSnapshot = CreateConfigurationSnapshot();
        var state = ApiReflection.Create("TNC.Trading.Platform.Api.Infrastructure.Persistence.AuthRuntimeStateEntity");
        var retryCycleId = Guid.NewGuid();
        var nextRetryAtUtc = DateTimeOffset.UtcNow.AddSeconds(1);

        ApiReflection.SetProperty(state, "RetryPhase", "InitialAutomatic");
        ApiReflection.SetProperty(state, "AutomaticAttemptNumber", 1);
        ApiReflection.SetProperty(state, "NextRetryAtUtc", nextRetryAtUtc);
        ApiReflection.SetProperty(state, "RetryLimitReached", false);

        _ = await ApiReflection.InvokeAsync(coordinator, "UpsertRetryCycleAsync", retryCycleId, configurationSnapshot, state, "Automatic", false, 1, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        ApiReflection.SetProperty(state, "RetryPhase", "Periodic");
        ApiReflection.SetProperty(state, "AutomaticAttemptNumber", 5);
        ApiReflection.SetProperty(state, "NextRetryAtUtc", nextRetryAtUtc.AddMinutes(5));
        ApiReflection.SetProperty(state, "RetryLimitReached", true);

        _ = await ApiReflection.InvokeAsync(coordinator, "UpsertRetryCycleAsync", retryCycleId, configurationSnapshot, state, "Automatic", true, 60, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var cycles = ((IEnumerable<object>)dbContext.GetType().GetProperty("AuthRetryCycles", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!.GetValue(dbContext)!)
            .ToArray();

        var cycle = Assert.Single(cycles);
        Assert.Equal("Periodic", ApiReflection.GetProperty<string>(cycle, "RetryPhase"));
        Assert.Equal(5, ApiReflection.GetProperty<int>(cycle, "AutomaticAttemptNumber"));
        Assert.True(ApiReflection.GetProperty<bool>(cycle, "RetryLimitReached"));
        Assert.True(ApiReflection.GetProperty<bool>(cycle, "FailureNotificationSent"));
        Assert.Equal(60, ApiReflection.GetProperty<int?>(cycle, "LastDelaySeconds"));
    }

    private static object CreateNotificationDispatcher(DbContext dbContext)
    {
        var providerType = ApiReflection.GetType("TNC.Trading.Platform.Api.Infrastructure.Notifications.INotificationProvider");
        var dispatcherType = ApiReflection.GetType("TNC.Trading.Platform.Api.Infrastructure.Platform.NotificationDispatcher");
        var emptyProviders = Array.CreateInstance(providerType, 0);

        return Activator.CreateInstance(
            dispatcherType,
            dbContext,
            emptyProviders,
            ApiReflection.CreateNullLogger(dispatcherType),
            TimeProvider.System)!;
    }

    private static object CreateConfigurationSnapshot()
    {
        return ApiReflection.Create(
            "TNC.Trading.Platform.Api.Configuration.PlatformConfigurationSnapshot",
            ApiReflection.ParseEnum("TNC.Trading.Platform.Api.Configuration.PlatformEnvironmentKind", "Live"),
            ApiReflection.ParseEnum("TNC.Trading.Platform.Api.Configuration.BrokerEnvironmentKind", "Demo"),
            ApiReflection.Create(
                "TNC.Trading.Platform.Api.Configuration.TradingScheduleConfiguration",
                new TimeOnly(8, 0),
                new TimeOnly(16, 30),
                new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                ApiReflection.ParseEnum("TNC.Trading.Platform.Api.Configuration.WeekendBehavior", "ExcludeWeekends"),
                Array.Empty<DateOnly>(),
                "UTC"),
            ApiReflection.Create(
                "TNC.Trading.Platform.Api.Configuration.RetryPolicyConfiguration",
                1,
                5,
                2,
                60,
                5),
            ApiReflection.Create(
                "TNC.Trading.Platform.Api.Configuration.NotificationSettingsConfiguration",
                "RecordedOnly",
                "owner@example.com"),
            ApiReflection.Create(
                "TNC.Trading.Platform.Api.Configuration.CredentialPresence",
                true,
                true,
                true),
            true,
            true,
            DateTimeOffset.UtcNow,
            false);
    }
}
