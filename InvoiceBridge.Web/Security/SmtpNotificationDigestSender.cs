using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.DTOs;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace InvoiceBridge.Web.Security;

public sealed class SmtpNotificationDigestSender(
    IOptionsMonitor<SmtpOptions> optionsMonitor,
    ILogger<SmtpNotificationDigestSender> logger) : INotificationDigestSender
{
    public async Task SendAsync(NotificationDigestMessage message, CancellationToken cancellationToken = default)
    {
        var options = optionsMonitor.CurrentValue;

        if (!options.Enabled)
        {
            logger.LogDebug("SMTP disabled; skipping delivery of digest '{Subject}' to {RecipientCount} recipients.",
                message.Subject, message.Recipients.Count);
            return;
        }

        if (message.Recipients.Count == 0)
        {
            return;
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromDisplayName, options.FromAddress));

        foreach (var recipient in message.Recipients.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            if (MailboxAddress.TryParse(recipient, out var address))
            {
                mime.Bcc.Add(address);
            }
            else
            {
                logger.LogWarning("Skipping malformed recipient '{Recipient}' for digest '{Subject}'.", recipient, message.Subject);
            }
        }

        if (mime.Bcc.Count == 0)
        {
            logger.LogWarning("No deliverable recipients for digest '{Subject}'; dropping.", message.Subject);
            return;
        }

        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        using var client = new SmtpClient
        {
            Timeout = options.TimeoutMilliseconds
        };

        var secureSocketOptions = options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

        await client.ConnectAsync(options.Host, options.Port, secureSocketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            await client.AuthenticateAsync(options.Username, options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Sent notification digest '{Subject}' to {RecipientCount} recipients via {Host}:{Port}.",
            message.Subject, mime.Bcc.Count, options.Host, options.Port);
    }
}
