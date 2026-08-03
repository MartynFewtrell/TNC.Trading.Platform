using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Platform;

namespace TNC.Trading.Platform.Infrastructure.Notifications;

internal static class NotificationDispatchPolicy
{
    public static NotificationDispatchContext CreateContext(
        string notificationType,
        string summary,
        PlatformConfigurationSnapshot configuration)
    {
        var sanitizedSummary = OperationalDataRedactor.RedactText(summary) ?? string.Empty;
        var recipient = string.IsNullOrWhiteSpace(configuration.NotificationSettings.EmailTo)
            ? "unconfigured"
            : configuration.NotificationSettings.EmailTo!;
        var providerName = configuration.NotificationSettings.Provider;

        return new NotificationDispatchContext(
            sanitizedSummary,
            recipient,
            providerName,
            new NotificationMessage(notificationType, recipient, sanitizedSummary));
    }
}