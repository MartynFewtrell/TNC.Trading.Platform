using System.Reflection;

namespace TNC.Trading.Platform.Api.UnitTests;

public class UpdatePlatformConfigurationValidatorTests
{
    [Fact]
    public void Validate_WithTestPlatformAndLiveBroker_ThrowsPlatformValidationException()
    {
        var validator = ApiReflection.Create("TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration.UpdatePlatformConfigurationValidator");
        var request = CreateRequest("Test", "Live", new TimeOnly(8, 0), new TimeOnly(16, 30));

        var exception = Assert.ThrowsAny<Exception>(() => ApiReflection.Invoke(validator, "Validate", request));
        Assert.Equal("PlatformValidationException", exception.GetType().Name);

        var errors = (IReadOnlyDictionary<string, string[]>)exception.GetType().GetProperty("Errors")!.GetValue(exception)!;
        Assert.Contains("BrokerEnvironment", errors.Keys);
    }

    [Fact]
    public void Validate_WithInvalidTradingWindow_ThrowsPlatformValidationException()
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
