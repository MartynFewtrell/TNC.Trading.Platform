namespace TNC.Trading.Platform.Infrastructure.Notifications;

internal sealed record NotificationDispatchContext(
    string SanitizedSummary,
    string Recipient,
    string ProviderName,
    NotificationMessage Message);