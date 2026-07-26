using Azure;
using Microsoft.Extensions.Logging;
using TNC.Trading.Platform.Application.Configuration;
using TNC.Trading.Platform.Infrastructure.Notifications;
using TNC.Trading.Platform.Infrastructure.Persistence;
using AppNotificationDispatcher = TNC.Trading.Platform.Application.Services.INotificationDispatcher;

namespace TNC.Trading.Platform.Infrastructure.Platform;

internal sealed class NotificationDispatcher(
    PlatformDbContext dbContext,
    IEnumerable<INotificationProvider> notificationProviders,
    ILogger<NotificationDispatcher> logger,
    TimeProvider timeProvider) : AppNotificationDispatcher
{
    public Task DispatchFailureAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) =>
        RecordAsync("AuthFailure", summary, configuration, correlationId, retryCycleId, cancellationToken);

    public Task DispatchRetryLimitReachedAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) =>
        RecordAsync("RetryLimitReached", summary, configuration, correlationId, retryCycleId, cancellationToken);

    public Task DispatchRecoveryAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) =>
        RecordAsync("AuthRecovered", summary, configuration, correlationId, retryCycleId, cancellationToken);

    public Task DispatchBlockedLiveAsync(PlatformConfigurationSnapshot configuration, string summary, string correlationId, Guid? retryCycleId, CancellationToken cancellationToken) =>
        RecordAsync("BlockedLiveAttempt", summary, configuration, correlationId, retryCycleId, cancellationToken);

    private async Task RecordAsync(
        string notificationType,
        string summary,
        PlatformConfigurationSnapshot configuration,
        string correlationId,
        Guid? retryCycleId,
        CancellationToken cancellationToken)
    {
        var dispatchContext = NotificationDispatchPolicy.CreateContext(notificationType, summary, configuration);
        var occurredAtUtc = timeProvider.GetUtcNow();
        var dispatchResult = await DispatchAsync(
            dispatchContext.Message,
            dispatchContext.ProviderName,
            cancellationToken).ConfigureAwait(false);

        dbContext.NotificationRecords.Add(new NotificationRecordEntity
        {
            NotificationType = notificationType,
            PlatformEnvironment = configuration.PlatformEnvironment.ToString(),
            BrokerEnvironment = configuration.BrokerEnvironment.ToString(),
            Recipient = dispatchContext.Recipient,
            Summary = dispatchContext.SanitizedSummary,
            DispatchStatus = dispatchResult.Status,
            Provider = dispatchResult.ProviderName,
            CorrelationId = correlationId,
            RetryCycleId = retryCycleId,
            DispatchedAtUtc = occurredAtUtc
        });

        dbContext.OperationalEvents.Add(new OperationalEventEntity
        {
            Category = "notification",
            EventType = notificationType,
            PlatformEnvironment = configuration.PlatformEnvironment.ToString(),
            BrokerEnvironment = configuration.BrokerEnvironment.ToString(),
            Severity = dispatchResult.Status == "Failed" ? "Error" : "Information",
            Summary = dispatchContext.SanitizedSummary,
            DetailsJson = OperationalDataRedactor.Serialize(new
            {
                Recipient = dispatchContext.Recipient,
                DispatchStatus = dispatchResult.Status,
                Provider = dispatchResult.ProviderName,
                retryCycleId
            }),
            CorrelationId = correlationId,
            RetryCycleId = retryCycleId,
            OccurredAtUtc = occurredAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Notification {DispatchStatus} for {NotificationType} to {Recipient}: {Summary}",
            dispatchResult.Status,
            notificationType,
                OperationalDataRedactor.RedactedValue,
                dispatchContext.SanitizedSummary);
    }

    private async Task<NotificationDispatchResult> DispatchAsync(
        NotificationMessage message,
        string providerName,
        CancellationToken cancellationToken)
    {
        if (string.Equals(message.Recipient, "unconfigured", StringComparison.Ordinal))
        {
            return new NotificationDispatchResult("Skipped", "Notification recipient is not configured.", providerName);
        }

        var provider = notificationProviders.SingleOrDefault(item =>
            string.Equals(item.Name, providerName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return new NotificationDispatchResult("Failed", $"Notification provider '{providerName}' is not registered.", providerName);
        }

        try
        {
            return await provider.DispatchAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Net.Mail.SmtpException or RequestFailedException)
        {
            var sanitizedMessage = OperationalDataRedactor.RedactText(exception.Message) ?? "Notification dispatch failed.";
            logger.LogError(
                "Notification provider {ProviderName} failed for {EventType}: {ErrorMessage}",
                providerName,
                message.EventType,
                sanitizedMessage);

            return new NotificationDispatchResult("Failed", sanitizedMessage, provider.Name);
        }
    }
}
