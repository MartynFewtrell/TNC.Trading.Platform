namespace TNC.Trading.Platform.Application.UnitTests;

public class RetryPolicyTimingTests
{
    /// <summary>
    /// Trace: FR14, TR2.
    /// Verifies: the default retry policy uses exponential backoff and respects the configured maximum delay.
    /// Expected: each attempt number in the theory data returns the expected capped exponential delay.
    /// Why: degraded-startup recovery depends on a predictable automatic retry cadence that operators can trust.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(8, 60)]
    public void GetDelayBeforeAttempt_ShouldUseExponentialBackoffWithCap_WhenDefaultRetryPolicyIsApplied(int attemptNumber, int expectedDelaySeconds)
    {
        var retryPolicy = ApplicationReflection.Create(
            "TNC.Trading.Platform.Application.Configuration.RetryPolicyConfiguration",
            1,
            5,
            2,
            60,
            5);

        var delay = (int)ApplicationReflection.InvokeStatic(
            "TNC.Trading.Platform.Application.Services.PlatformStateCoordinator",
            "GetDelayBeforeAttempt",
            retryPolicy,
            attemptNumber)!;

        Assert.Equal(expectedDelaySeconds, delay);
    }

    /// <summary>
    /// Trace: FR14, TR2.
    /// Verifies: custom retry-policy values override the default delay profile while still honoring the configured cap.
    /// Expected: each attempt number in the theory data returns the configured delay progression and cap.
    /// Why: operator-managed retry tuning must remain accurate so recovery timing reflects persisted configuration.
    /// </summary>
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(2, 9)]
    [InlineData(3, 20)]
    [InlineData(4, 20)]
    public void GetDelayBeforeAttempt_ShouldRespectConfiguredDelayMultiplierAndCap_WhenCustomRetryPolicyIsApplied(int attemptNumber, int expectedDelaySeconds)
    {
        var retryPolicy = ApplicationReflection.Create(
            "TNC.Trading.Platform.Application.Configuration.RetryPolicyConfiguration",
            3,
            5,
            3,
            20,
            7);

        var delay = (int)ApplicationReflection.InvokeStatic(
            "TNC.Trading.Platform.Application.Services.PlatformStateCoordinator",
            "GetDelayBeforeAttempt",
            retryPolicy,
            attemptNumber)!;

        Assert.Equal(expectedDelaySeconds, delay);
    }
}
