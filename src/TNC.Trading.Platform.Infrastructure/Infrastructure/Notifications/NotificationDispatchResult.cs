namespace TNC.Trading.Platform.Infrastructure.Notifications;

internal sealed record NotificationDispatchResult(
    string Status,
    string Summary,
    string ProviderName);
