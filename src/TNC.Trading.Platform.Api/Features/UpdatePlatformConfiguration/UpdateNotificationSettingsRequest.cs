namespace TNC.Trading.Platform.Api.Features.UpdatePlatformConfiguration;

internal sealed record UpdateNotificationSettingsRequest(
    string Provider,
    string? EmailTo);
