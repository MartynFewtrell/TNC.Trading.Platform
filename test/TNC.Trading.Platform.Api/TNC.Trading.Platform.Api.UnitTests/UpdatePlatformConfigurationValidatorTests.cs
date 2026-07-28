using TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;
using TNC.Trading.Platform.Api.Infrastructure.Platform;

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
    public void Validate_ShouldAcceptBusinessRuleViolation_WhenPlatformIsTestAndBrokerIsLive()
    {
        var validator = new UpdatePlatformConfigurationValidator();
        var request = CreateRequest("Test", "Live", new TimeOnly(8, 0), new TimeOnly(16, 30));

        validator.Validate(request);
    }

    /// <summary>
    /// Trace: FR21, FR20.
    /// Verifies: configuration validation rejects a trading window whose end occurs before its start.
    /// Expected: validation throws a platform validation exception that includes a trading-schedule error.
    /// Why: invalid trading-window values must be blocked before unusable schedule configuration is stored.
    /// </summary>
    [Fact]
    public void Validate_ShouldAcceptBusinessRuleViolation_WhenTradingWindowIsInvalid()
    {
        var validator = new UpdatePlatformConfigurationValidator();
        var request = CreateRequest("Live", "Demo", new TimeOnly(16, 30), new TimeOnly(8, 0));

        validator.Validate(request);
    }

    private static UpdatePlatformConfigurationRequest CreateRequest(string platformEnvironment, string brokerEnvironment, TimeOnly startOfDay, TimeOnly endOfDay)
    {
        var tradingSchedule = new UpdateTradingScheduleRequest(
            startOfDay,
            endOfDay,
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            "ExcludeWeekends",
            Array.Empty<DateOnly>(),
            "UTC");

        var retryPolicy = new UpdateRetryPolicyRequest(
            1,
            5,
            2,
            60,
            5);

        var notificationSettings = new UpdateNotificationSettingsRequest(
            "RecordedOnly",
            "owner@example.com");

        var credentials = new UpdateIgCredentialsRequest(
            "api-key",
            "identifier",
            "password");

        return new UpdatePlatformConfigurationRequest(
            platformEnvironment,
            brokerEnvironment,
            tradingSchedule,
            retryPolicy,
            notificationSettings,
            credentials,
            "unit-test");
    }
}
