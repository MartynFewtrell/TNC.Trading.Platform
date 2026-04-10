namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed record UpdatedNotificationSettingsResponse(
    string Provider,
    string? EmailTo);
