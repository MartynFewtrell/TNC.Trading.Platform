using TNC.Trading.Platform.Application.Configuration;

namespace TNC.Trading.Platform.Application.Services;

internal static class RetryTimingPolicy
{
    public static int CalculateDelayBeforeAttempt(RetryPolicyConfiguration configuration, int attemptNumber)
    {
        if (attemptNumber <= 1)
        {
            return configuration.InitialDelaySeconds;
        }

        var delaySeconds = configuration.InitialDelaySeconds;
        for (var index = 1; index < attemptNumber; index++)
        {
            var nextDelaySeconds = (long)delaySeconds * configuration.Multiplier;
            if (nextDelaySeconds >= configuration.MaxDelaySeconds)
            {
                return configuration.MaxDelaySeconds;
            }

            delaySeconds = (int)nextDelaySeconds;
        }

        return delaySeconds;
    }
}