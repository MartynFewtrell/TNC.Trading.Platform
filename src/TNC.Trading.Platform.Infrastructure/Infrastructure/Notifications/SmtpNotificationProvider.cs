using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TNC.Trading.Platform.Infrastructure.Notifications;

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
