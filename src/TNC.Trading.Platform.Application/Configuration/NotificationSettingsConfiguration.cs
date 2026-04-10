namespace TNC.Trading.Platform.Application.Configuration;

internal sealed record NotificationSettingsConfiguration(
    string Provider,
    string? EmailTo);
