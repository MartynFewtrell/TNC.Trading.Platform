using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TNC.Trading.Platform.Infrastructure.Notifications;

internal sealed class AzureCommunicationServicesEmailNotificationProvider(
    IConfiguration configuration,
    ILogger<AzureCommunicationServicesEmailNotificationProvider> logger) : INotificationProvider
{
    public string Name => "AzureCommunicationServicesEmail";

    public async Task<NotificationDispatchResult> DispatchAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var senderAddress = GetSetting(
            "NotificationTransports:AzureCommunicationServices:SenderAddress",
            "Notifications:AzureCommunicationServices:SenderAddress");
        var endpoint = GetSetting(
            "NotificationTransports:AzureCommunicationServices:Endpoint",
            "Notifications:AzureCommunicationServices:Endpoint");
        var connectionString = GetSetting(
            "NotificationTransports:AzureCommunicationServices:ConnectionString",
            "Notifications:AzureCommunicationServices:ConnectionString");

        if (string.IsNullOrWhiteSpace(senderAddress)
            || (string.IsNullOrWhiteSpace(endpoint) && string.IsNullOrWhiteSpace(connectionString)))
        {
            return new NotificationDispatchResult(
                "Skipped",
                "Azure Communication Services Email transport is not configured.",
                Name);
        }

        var client = CreateClient(endpoint, connectionString);
        var emailMessage = new EmailMessage(
            senderAddress,
            new EmailRecipients([new EmailAddress(message.Recipient)]),
            new EmailContent($"TNC Trading Platform - {message.EventType}")
            {
                PlainText = message.Summary
            });

        var sendOperation = await client.SendAsync(Azure.WaitUntil.Completed, emailMessage, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "ACS email notification {EventType} sent to {Recipient} with operation {OperationId}",
            message.EventType,
            message.Recipient,
            sendOperation.Id);

        return new NotificationDispatchResult("Sent", message.Summary, Name);
    }

    private EmailClient CreateClient(string? endpoint, string? connectionString)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new EmailClient(connectionString);
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            throw new InvalidOperationException("A valid Azure Communication Services endpoint must be configured.");
        }

        return new EmailClient(endpointUri, new DefaultAzureCredential());
    }

    private string? GetSetting(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
