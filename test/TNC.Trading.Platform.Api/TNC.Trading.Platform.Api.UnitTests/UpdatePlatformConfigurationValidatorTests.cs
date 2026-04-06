using System.Reflection;

namespace TNC.Trading.Platform.Api.UnitTests;

public class UpdatePlatformConfigurationValidatorTests
{
    /// <summary>
    /// Trace: FR8, FR20, SR4.
    /// Verifies: configuration validation rejects a live broker selection when the platform environment is Test.
    /// Expected: validation throws a platform validation exception that includes a broker-environment error.
    /// Why: the Test-platform safeguard must prevent unsafe live activation before configuration can be persisted.
    /// </summary>
    [Fact]
    public void Validate_ShouldThrowPlatformValidationException_WhenPlatformIsTestAndBrokerIsLive()
    {
        var validator = ApiReflection.Create("TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration.UpdatePlatformConfigurationValidator");
        var request = CreateRequest("Test", "Live", new TimeOnly(8, 0), new TimeOnly(16, 30));

        var exception = Assert.ThrowsAny<Exception>(() => ApiReflection.Invoke(validator, "Validate", request));
        Assert.Equal("PlatformValidationException", exception.GetType().Name);

        var errors = (IReadOnlyDictionary<string, string[]>)exception.GetType().GetProperty("Errors")!.GetValue(exception)!;
        Assert.Contains("BrokerEnvironment", errors.Keys);
    }

    /// <summary>
    /// Trace: FR21, FR20.
    /// Verifies: configuration validation rejects a trading window whose end occurs before its start.
    /// Expected: validation throws a platform validation exception that includes a trading-schedule error.
    /// Why: invalid trading-window values must be blocked before unusable schedule configuration is stored.
    /// </summary>
    [Fact]
    public void Validate_ShouldThrowPlatformValidationException_WhenTradingWindowIsInvalid()
    {
        var validator = ApiReflection.Create("TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration.UpdatePlatformConfigurationValidator");
        var request = CreateRequest("Live", "Demo", new TimeOnly(16, 30), new TimeOnly(8, 0));

        var exception = Assert.ThrowsAny<Exception>(() => ApiReflection.Invoke(validator, "Validate", request));
        Assert.Equal("PlatformValidationException", exception.GetType().Name);

        var errors = (IReadOnlyDictionary<string, string[]>)exception.GetType().GetProperty("Errors")!.GetValue(exception)!;
        Assert.Contains("TradingSchedule", errors.Keys.Single());
    }

    private static object CreateRequest(string platformEnvironment, string brokerEnvironment, TimeOnly startOfDay, TimeOnly endOfDay)
    {
        var tradingSchedule = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration.UpdateTradingScheduleRequest",
            startOfDay,
            endOfDay,
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            "ExcludeWeekends",
            Array.Empty<DateOnly>(),
            "UTC");

        var retryPolicy = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration.UpdateRetryPolicyRequest",
            1,
            5,
            2,
            60,
            5);

        var notificationSettings = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration.UpdateNotificationSettingsRequest",
            "RecordedOnly",
            "owner@example.com");

        var credentials = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration.UpdateIgCredentialsRequest",
            "api-key",
            "identifier",
            "password");

        return ApiReflection.Create(
            "TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration.UpdatePlatformConfigurationRequest",
            platformEnvironment,
            brokerEnvironment,
            tradingSchedule,
            retryPolicy,
            notificationSettings,
            credentials,
            "unit-test");
    }
}
