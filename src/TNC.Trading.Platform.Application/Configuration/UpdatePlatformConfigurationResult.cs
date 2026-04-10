namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record UpdatePlatformConfigurationResult(
    PlatformConfigurationSnapshot Snapshot,
    bool RestartRequired);
