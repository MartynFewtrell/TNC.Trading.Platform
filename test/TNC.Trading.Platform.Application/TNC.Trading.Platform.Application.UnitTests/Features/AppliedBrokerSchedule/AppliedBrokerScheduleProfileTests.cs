using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Features.AppliedBrokerSchedule;
using TNC.Trading.Platform.Application.Features.UpdatePlatformConfiguration;

namespace TNC.Trading.Platform.Application.UnitTests.Features.AppliedBrokerSchedule;

public sealed class AppliedBrokerScheduleProfileTests
{
    /// <summary>
    /// Trace: Trading-Day Market Data Work Item 1, applied schedule editing.
    /// Verifies a same-day window and named zone are accepted by the shared Trading Day validator.
    /// Expected: validation completes without error for an eligible weekday calendar.
    /// Why: the applied profile is the single configurable schedule authority and must accept ordinary valid operator edits.
    /// </summary>
    [Fact]
    public void Validate_ShouldAcceptValidNamedZoneAndSameDayWindow_WhenScheduleIsUpdated()
    {
        var schedule = CreateSchedule();

        new AppliedBrokerScheduleProfileValidator().Validate(schedule);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1.
    /// Verifies the validator rejects an end time that is not later than the start time.
    /// Expected: a field-specific validation error is raised before the schedule store can be called.
    /// Why: the profile contract represents one same-day window and must never persist an overnight or zero-length window.
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectInvalidWindow_WhenScheduleEndIsNotLaterThanStart()
    {
        var schedule = CreateSchedule() with
        {
            StartOfDay = new TimeOnly(17, 0),
            EndOfDay = new TimeOnly(8, 0)
        };

        var exception = Assert.Throws<ConfigurationValidationException>(
            () => new AppliedBrokerScheduleProfileValidator().Validate(schedule));

        Assert.Contains(nameof(TradingScheduleConfiguration.StartOfDay), exception.Errors.Keys);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, Work Item 1.
    /// Verifies malformed calendars and unknown time zones are rejected together.
    /// Expected: validation reports both configuration fields.
    /// Why: invalid persisted calendar inputs would make the shared schedule unsafe to evaluate.
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectInvalidCalendarAndZone_WhenScheduleIsUpdated()
    {
        var schedule = CreateSchedule() with
        {
            TradingDays = [],
            TimeZone = "Not/A-Time-Zone"
        };

        var exception = Assert.Throws<ConfigurationValidationException>(
            () => new AppliedBrokerScheduleProfileValidator().Validate(schedule));

        Assert.Contains(nameof(TradingScheduleConfiguration.TradingDays), exception.Errors.Keys);
        Assert.Contains(nameof(TradingScheduleConfiguration.TimeZone), exception.Errors.Keys);
    }

    /// <summary>
    /// Trace: Trading-Day Market Data Delivery Plan, applied-versus-selected schedule contract.
    /// Verifies a valid schedule update reaches the store as schedule data only; the caller cannot nominate a broker.
    /// Expected: the store receives the validated window and the returned applied profile is passed through.
    /// Why: an unapplied broker selection must never become the edit target through client-supplied identity.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldSaveScheduleWithoutClientBrokerIdentity_WhenRequestIsValid()
    {
        var store = new RecordingAppliedBrokerScheduleProfileStore();
        var handler = new UpdateAppliedBrokerScheduleProfileHandler(store, new AppliedBrokerScheduleProfileValidator());
        var schedule = CreateSchedule();

        var response = await handler.HandleAsync(
            new UpdateAppliedBrokerScheduleProfileRequest(schedule, "unit-operator"),
            CancellationToken.None);

        Assert.Equal(schedule, store.SavedSchedule);
        Assert.Equal("unit-operator", store.SavedActor);
        Assert.Equal(store.Profile.BrokerEnvironmentId, response.Profile.BrokerEnvironmentId);
        Assert.Equal(schedule, response.Profile.TradingSchedule);
    }

    private static TradingScheduleConfiguration CreateSchedule() => new(
        new TimeOnly(8, 0),
        new TimeOnly(17, 0),
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        WeekendBehavior.ExcludeWeekends,
        [],
        "Europe/London");

    private sealed class RecordingAppliedBrokerScheduleProfileStore : IAppliedBrokerScheduleProfileStore
    {
        public TradingScheduleConfiguration? SavedSchedule { get; private set; }

        public string? SavedActor { get; private set; }

        public AppliedBrokerScheduleProfile Profile { get; } = new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            3,
            CreateSchedule());

        public Task<AppliedBrokerScheduleProfile> GetAppliedAsync(CancellationToken cancellationToken)
            => Task.FromResult(Profile);

        public Task<AppliedBrokerScheduleProfile> SaveAppliedAsync(
            TradingScheduleConfiguration tradingSchedule,
            string actor,
            CancellationToken cancellationToken)
        {
            SavedSchedule = tradingSchedule;
            SavedActor = actor;
            return Task.FromResult(Profile with { TradingSchedule = tradingSchedule });
        }
    }
}
