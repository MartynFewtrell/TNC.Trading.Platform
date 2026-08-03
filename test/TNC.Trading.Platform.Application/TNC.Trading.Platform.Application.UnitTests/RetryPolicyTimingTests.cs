using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Application.Services;

namespace TNC.Trading.Platform.Application.UnitTests;

public class RetryPolicyTimingTests
{
    /// <summary>
    /// Trace: FR14, TR2.
    /// Verifies: the first automatic retry uses the configured initial delay.
    /// Expected: attempt one returns the initial delay without applying the multiplier.
    /// Why: degraded-startup recovery must begin at the operator-configured cadence.
    /// </summary>
    [Fact]
    public void CalculateDelayBeforeAttempt_ShouldReturnInitialDelay_WhenFirstAttemptIsScheduled()
    {
        var configuration = CreateConfiguration();

        var delaySeconds = RetryTimingPolicy.CalculateDelayBeforeAttempt(configuration, 1);

        Assert.Equal(3, delaySeconds);
    }

    /// <summary>
    /// Trace: FR14, TR2.
    /// Verifies: the established default retry profile retains its exact exponential delays and cap.
    /// Expected: each default attempt returns the same delay characterized before policy extraction.
    /// Why: moving ownership must not change the production cadence operators already rely on.
    /// </summary>
    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(8, 60)]
    public void CalculateDelayBeforeAttempt_ShouldPreserveDefaultProfile_WhenPolicyOwnershipMoves(int attemptNumber, int expectedDelaySeconds)
    {
        var configuration = new RetryPolicyConfiguration(1, 5, 2, 60, 5);

        var delaySeconds = RetryTimingPolicy.CalculateDelayBeforeAttempt(configuration, attemptNumber);

        Assert.Equal(expectedDelaySeconds, delaySeconds);
    }

    /// <summary>
    /// Trace: FR14, TR2.
    /// Verifies: automatic retry delays progress exponentially from the configured initial delay.
    /// Expected: attempts before the cap return the initial delay multiplied once per prior attempt.
    /// Why: recovery attempts need predictable bounded spacing without depending on a host or timer.
    /// </summary>
    [Theory]
    [InlineData(2, 9)]
    [InlineData(3, 27)]
    public void CalculateDelayBeforeAttempt_ShouldReturnExponentialProgression_WhenAttemptIsBelowMaximumDelay(int attemptNumber, int expectedDelaySeconds)
    {
        var configuration = CreateConfiguration();

        var delaySeconds = RetryTimingPolicy.CalculateDelayBeforeAttempt(configuration, attemptNumber);

        Assert.Equal(expectedDelaySeconds, delaySeconds);
    }

    /// <summary>
    /// Trace: FR14, TR2.
    /// Verifies: exponential retry progression never exceeds the configured maximum delay.
    /// Expected: attempts at and beyond the cap return the maximum delay.
    /// Why: automatic recovery must remain bounded even for large attempt numbers.
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(int.MaxValue)]
    public void CalculateDelayBeforeAttempt_ShouldReturnMaximumDelay_WhenExponentialProgressionReachesCap(int attemptNumber)
    {
        var configuration = CreateConfiguration();

        var delaySeconds = RetryTimingPolicy.CalculateDelayBeforeAttempt(configuration, attemptNumber);

        Assert.Equal(60, delaySeconds);
    }

    /// <summary>
    /// Trace: FR14, TR2.
    /// Verifies: resetting the attempt counter preserves the existing initial-delay behavior.
    /// Expected: a non-positive attempt number returns the initial delay rather than multiplying or exhausting retries.
    /// Why: manual and renewed retry cycles reset their counter before scheduling the first attempt.
    /// </summary>
    [Fact]
    public void CalculateDelayBeforeAttempt_ShouldReturnInitialDelay_WhenAttemptCounterIsReset()
    {
        var configuration = CreateConfiguration();

        var delaySeconds = RetryTimingPolicy.CalculateDelayBeforeAttempt(configuration, 0);

        Assert.Equal(3, delaySeconds);
    }

    private static RetryPolicyConfiguration CreateConfiguration() =>
        new(
            InitialDelaySeconds: 3,
            MaxAutomaticRetries: 5,
            Multiplier: 3,
            MaxDelaySeconds: 60,
            PeriodicDelayMinutes: 7);
}
