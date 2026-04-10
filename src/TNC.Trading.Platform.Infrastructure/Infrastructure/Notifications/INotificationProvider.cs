namespace TNC.Trading.Platform.Infrastructure.Notifications;

internal interface INotificationProvider
{
    string Name { get; }

    Task<NotificationDispatchResult> DispatchAsync(NotificationMessage message, CancellationToken cancellationToken);
}
