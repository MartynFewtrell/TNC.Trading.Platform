namespace TNC.Trading.Platform.Application.Configuration;

public sealed class PlatformEnvironmentContext(PlatformEnvironmentKind environment) : IPlatformEnvironmentContext
{
    public PlatformEnvironmentKind Environment { get; } = environment;
}