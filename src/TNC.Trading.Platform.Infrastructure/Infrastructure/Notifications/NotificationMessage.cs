namespace TNC.Trading.Platform.Infrastructure.Notifications;

internal sealed record NotificationMessage(
    string EventType,
    string Recipient,
    string Summary);
