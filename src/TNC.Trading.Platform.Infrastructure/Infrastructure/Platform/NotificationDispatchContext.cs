using TNC.Trading.Platform.Infrastructure.Notifications;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed record NotificationDispatchContext(
    string SanitizedSummary,
    string Recipient,
    string ProviderName,
    NotificationMessage Message);