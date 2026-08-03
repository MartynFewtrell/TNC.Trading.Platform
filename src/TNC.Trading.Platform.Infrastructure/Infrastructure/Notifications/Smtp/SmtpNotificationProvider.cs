using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TNC.Trading.Platform.Infrastructure.Notifications.Smtp;

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

        // SmtpClient.Timeout only applies to synchronous operations; SendMailAsync ignores it.
        // Use an internal deadline so startup-time notification dispatch cannot hang indefinitely
        // when the SMTP relay is slow or unresponsive.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await smtpClient.SendMailAsync(mailMessage, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "SMTP notification {EventType} timed out after 10 s and was skipped.",
                message.EventType);
            return new NotificationDispatchResult("TimedOut", "SMTP dispatch timed out.", Name);
        }

        logger.LogInformation(
            "SMTP notification {EventType} sent via {Host}:{Port}",
            message.EventType,
            host,
            port);

        return new NotificationDispatchResult("Sent", message.Summary, Name);
    }
}