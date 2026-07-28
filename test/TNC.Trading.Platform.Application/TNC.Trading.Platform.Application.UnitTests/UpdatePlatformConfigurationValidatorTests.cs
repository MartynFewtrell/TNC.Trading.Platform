using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

namespace TNC.Trading.Platform.Application.UnitTests;

public sealed class UpdatePlatformConfigurationValidatorTests
{
    /// <summary>
    /// Trace: FR20, FR21. Verifies an Application caller cannot persist a schedule without trading days.
    /// Expected: validation reports the trading-day field. Why: non-HTTP callers must receive the same business safeguard.
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectEmptyTradingDays_WhenConfigurationIsUpdated()
    {
        var exception = Assert.Throws<ConfigurationValidationException>(() => new UpdatePlatformConfigurationValidator().Validate(CreateUpdate(tradingDays: [])));

        Assert.Contains("TradingSchedule.TradingDays", exception.Errors.Keys);
    }

    /// <summary>
    /// Trace: FR20, FR21. Verifies the Application rejects a time-zone identifier unavailable to the runtime.
    /// Expected: validation reports the time-zone field. Why: schedule evaluation must not receive an unknown zone.
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectUnknownTimeZone_WhenConfigurationIsUpdated()
    {
        var exception = Assert.Throws<ConfigurationValidationException>(() => new UpdatePlatformConfigurationValidator().Validate(CreateUpdate(timeZone: "Not/A-Time-Zone")));

        Assert.Contains("TradingSchedule.TimeZone", exception.Errors.Keys);
    }

    /// <summary>
    /// Trace: FR12, FR20. Verifies retry bounds are enforced before persistence.
    /// Expected: validation reports MaxDelaySeconds when it is below InitialDelaySeconds. Why: retry scheduling must remain bounded.
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectMaxDelayBelowInitialDelay_WhenRetryPolicyIsInvalid()
    {
        var exception = Assert.Throws<ConfigurationValidationException>(() => new UpdatePlatformConfigurationValidator().Validate(CreateUpdate(initialDelaySeconds: 60, maxDelaySeconds: 30)));

        Assert.Contains("RetryPolicy.MaxDelaySeconds", exception.Errors.Keys);
    }

    private static PlatformConfigurationUpdate CreateUpdate(
        IReadOnlyList<DayOfWeek>? tradingDays = null,
        string timeZone = "UTC",
        int initialDelaySeconds = 1,
        int maxDelaySeconds = 60)
        => new(
            PlatformEnvironmentKind.Live,
            BrokerEnvironmentKind.Demo,
            new TradingScheduleConfiguration(new TimeOnly(8, 0), new TimeOnly(16, 30), tradingDays ?? [DayOfWeek.Monday], WeekendBehavior.ExcludeWeekends, [], timeZone),
            new RetryPolicyConfiguration(initialDelaySeconds, 5, 2, maxDelaySeconds, 5),
            new NotificationSettingsConfiguration("RecordedOnly", "operator@example.com"),
            "api-key",
            "identifier",
            "password",
            "unit-test");
}