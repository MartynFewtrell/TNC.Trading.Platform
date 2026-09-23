namespace TNC.Trading.Platform.Application.Configuration;

public sealed class PlatformEnvironmentOptions
{
    public string? Environment { get; set; }

    public PlatformEnvironmentKind GetValidatedEnvironment()
    {
        if (!Enum.TryParse<PlatformEnvironmentKind>(Environment, ignoreCase: true, out var environment)
            || !Enum.IsDefined(environment))
        {
            throw new InvalidOperationException(
                "Platform:Environment must be one of Desktop, Development, Test, or Live.");
        }

        return environment;
    }
}