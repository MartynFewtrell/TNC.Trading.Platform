namespace TNC.Trading.Platform.Application.Configuration;

public interface IPlatformEnvironmentContext
{
    PlatformEnvironmentKind Environment { get; }
}