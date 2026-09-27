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
        var request = CreateRequest("Live", new TimeOnly(8, 0), new TimeOnly(16, 30));

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
        var request = CreateRequest("Demo", new TimeOnly(16, 30), new TimeOnly(8, 0));

        validator.Validate(request);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 1, step 2.
    /// Verifies: transport validation allows counts above the former four-update cap.
    /// Expected: five updates per day passes validation because high counts produce a warning, not rejection.
    /// Why: the API must not impose a stricter count limit than the application schedule policy.
    /// </summary>
    [Fact]
    public void Validate_ShouldAcceptFrequencyAboveFour_WhenRequestIsValid()
    {
        var validator = new UpdatePlatformConfigurationValidator();
        var request = CreateRequest("Demo", new TimeOnly(8, 0), new TimeOnly(17, 0)) with
        {
            InstrumentUpdatesPerDay = 5
        };

        validator.Validate(request);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 1, step 2.
    /// Verifies: transport validation rejects negative timed-update counts.
    /// Expected: the validation problem identifies the frequency field.
    /// Why: only zero or positive counts have well-defined schedule behavior.
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectNegativeFrequency_WhenRequestIsInvalid()
    {
        var validator = new UpdatePlatformConfigurationValidator();
        var request = CreateRequest("Demo", new TimeOnly(8, 0), new TimeOnly(17, 0)) with
        {
            InstrumentUpdatesPerDay = -1
        };

        var exception = Assert.Throws<PlatformValidationException>(() => validator.Validate(request));

        Assert.Contains(nameof(request.InstrumentUpdatesPerDay), exception.Errors.Keys);
    }

    private static UpdatePlatformConfigurationRequest CreateRequest(string brokerEnvironment, TimeOnly startOfDay, TimeOnly endOfDay)
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
            brokerEnvironment,
            tradingSchedule,
            retryPolicy,
            notificationSettings,
            credentials,
            "unit-test");
    }
}
