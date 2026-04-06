namespace TNC.Trading.Platform.Api.Features.GetPlatformConfiguration;

internal sealed record ConfigurationNotificationSettingsResponse(
    string Provider,
    string? EmailTo);
