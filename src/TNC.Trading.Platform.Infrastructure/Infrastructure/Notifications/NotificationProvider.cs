using Azure.Communication.Email;
using Azure.Identity;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TNC.Trading.Platform.Infrastructure.Notifications;

internal sealed record NotificationMessage(
    string EventType,
    string Recipient,
    string Summary);

internal sealed record NotificationDispatchResult(
    string Status,
    string Summary,
    string ProviderName);

internal interface INotificationProvider
{
    string Name { get; }

    Task<NotificationDispatchResult> DispatchAsync(NotificationMessage message, CancellationToken cancellationToken);
}

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

internal sealed class SmtpNotificationProvider(
    IConfiguration configuration,
    ILogger<SmtpNotificationProvider> logger) : INotificationProvider
{
    public string Name => "Smtp";

    public async Task<NotificationDispatchResult> DispatchAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var host = configuration["NotificationTransports:Smtp:Host"];
        var senderAddress = configuration["NotificationTransports:Smtp:SenderAddress"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(senderAddress))
        {
            return new NotificationDispatchResult("Skipped", "SMTP notification transport is not configured.", Name);
        }

        var port = int.TryParse(configuration["NotificationTransports:Smtp:Port"], out var configuredPort)
            ? configuredPort
            : 1025;
        var enableSsl = bool.TryParse(configuration["NotificationTransports:Smtp:EnableSsl"], out var configuredSsl)
            && configuredSsl;

        using var smtpClient = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = CredentialCache.DefaultNetworkCredentials
        };

        using var mailMessage = new MailMessage(senderAddress, message.Recipient)
        {
            Subject = $"TNC Trading Platform - {message.EventType}",
            Body = message.Summary,
            IsBodyHtml = false
        };

        await smtpClient.SendMailAsync(mailMessage, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "SMTP notification {EventType} sent to {Recipient} via {Host}:{Port}",
            message.EventType,
            message.Recipient,
            host,
            port);

        return new NotificationDispatchResult("Sent", message.Summary, Name);
    }
}

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
