namespace TNC.Trading.Platform.Application.UnitTests;

public class RetryPolicyTimingTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(8, 60)]
    public void GetDelayBeforeAttempt_UsesExponentialBackoffWithCap(int attemptNumber, int expectedDelaySeconds)
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
}
