using TNC.Trading.Platform.Application.Features.AccountDetails;
using TNC.Trading.Platform.Application.Services;
using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.UnitTests.Features.AccountDetails;

public sealed class AccountDetailsContractTests
{
    /// <summary>
    /// Trace: Account Details Phase 1.2 trading-day requirement.
    /// Verifies: the application derives the day in the configured schedule time zone rather than UTC.
    /// Expected: a UTC instant before local midnight belongs to the next configured local trading day.
    /// Why: automatic daily de-duplication must follow operator schedule semantics across UTC boundaries.
    /// </summary>
    [Fact]
    public void GetTradingDay_ShouldUseConfiguredTimeZone_WhenUtcDateDiffersFromLocalDate()
    {
        var gate = new TradingScheduleGate();
        var schedule = new TradingScheduleConfiguration(
            new TimeOnly(8),
            new TimeOnly(17),
            [],
            WeekendBehavior.ExcludeWeekends,
            [],
            "America/New_York");

        var result = gate.GetTradingDay(schedule, new DateTimeOffset(2026, 8, 8, 3, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 8, 7), result);
    }

    /// <summary>
    /// Trace: Account Details Phase 1.1 deterministic-history requirement.
    /// Verifies: the opaque cursor preserves both retrieval timestamp and identifier.
    /// Expected: a valid cursor decodes to the exact composite key used for keyset navigation.
    /// Why: equal timestamps must not cause skipped or repeated history entries.
    /// </summary>
    [Fact]
    public void Cursor_ShouldRoundTripTimestampAndIdentifier_WhenEncoded()
    {
        var expected = new AccountDetailsCursor(new DateTimeOffset(2026, 8, 7, 12, 30, 0, TimeSpan.Zero), Guid.NewGuid());

        var isValid = AccountDetailsCursor.TryDecode(expected.Encode(), out var actual);

        Assert.True(isValid);
        Assert.Equal(expected, actual);
    }
}
