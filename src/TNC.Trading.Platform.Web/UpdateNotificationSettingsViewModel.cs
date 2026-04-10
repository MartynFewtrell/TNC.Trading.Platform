namespace TNC.Trading.Platform.Web;

internal sealed class UpdateNotificationSettingsViewModel
{
    public string Provider { get; set; } = string.Empty;

    public string? EmailTo { get; set; }
}
