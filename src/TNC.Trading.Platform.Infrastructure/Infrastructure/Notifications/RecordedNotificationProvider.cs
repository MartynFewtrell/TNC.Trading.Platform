using Microsoft.Extensions.Logging;

namespace TNC.Trading.Platform.Infrastructure.Notifications;

internal sealed class RecordedNotificationProvider(ILogger<RecordedNotificationProvider> logger) : INotificationProvider
{
    public string Name => "RecordedOnly";

    public Task<NotificationDispatchResult> DispatchAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Recorded notification {EventType} for {Recipient}: {Summary}",
            message.EventType,
            message.Recipient,
            message.Summary);

        return Task.FromResult(new NotificationDispatchResult("Recorded", message.Summary, Name));
    }
}
