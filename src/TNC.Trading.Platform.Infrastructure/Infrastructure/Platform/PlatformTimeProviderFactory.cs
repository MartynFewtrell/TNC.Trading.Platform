using Microsoft.Extensions.Configuration;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal static class PlatformTimeProviderFactory
{
    public static TimeProvider Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var mode = configuration["Bootstrap:TimeProvider:Mode"];
        if (!string.Equals(mode, "Incrementing", StringComparison.OrdinalIgnoreCase))
        {
            return TimeProvider.System;
        }

        var startUtc = DateTimeOffset.TryParse(configuration["Bootstrap:TimeProvider:StartUtc"], out var configuredStartUtc)
            ? configuredStartUtc.ToUniversalTime()
            : DateTimeOffset.UtcNow;
        var stepSeconds = int.TryParse(configuration["Bootstrap:TimeProvider:StepSeconds"], out var configuredStepSeconds) && configuredStepSeconds > 0
            ? configuredStepSeconds
            : 1;

        return new IncrementingTimeProvider(startUtc, TimeSpan.FromSeconds(stepSeconds));
    }
}
