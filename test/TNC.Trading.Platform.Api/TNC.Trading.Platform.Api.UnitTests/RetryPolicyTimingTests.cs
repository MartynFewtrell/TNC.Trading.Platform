namespace TNC.Trading.Platform.Api.UnitTests;

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
        var retryPolicy = ApiReflection.Create(
            "TNC.Trading.Platform.Api.Configuration.RetryPolicyConfiguration",
            1,
            5,
            2,
            60,
            5);

        var delay = (int)ApiReflection.InvokeStatic(
            "TNC.Trading.Platform.Api.Infrastructure.Platform.PlatformStateCoordinator",
            "GetDelayBeforeAttempt",
            retryPolicy,
            attemptNumber)!;

        Assert.Equal(expectedDelaySeconds, delay);
    }
}
