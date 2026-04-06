namespace TNC.Trading.Platform.Web;

internal sealed record NotificationSettingsViewModel(
    string Provider,
    string? EmailTo);
