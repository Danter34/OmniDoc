using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Infrastructure.Common.Settings;

namespace OmniDoc.Infrastructure.Services.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    Func<ISmtpClient> clientFactory,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        try
        {
            using var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = clientFactory();
            var security = settings.Port switch
            {
                587 => SecureSocketOptions.StartTls,
                465 => SecureSocketOptions.SslOnConnect,
                1025 => SecureSocketOptions.None,
                _ => settings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None
            };
            await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken);

            if (!string.IsNullOrWhiteSpace(settings.UserName) &&
                !string.IsNullOrWhiteSpace(settings.Password))
            {
                await client.AuthenticateAsync(settings.UserName, settings.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Server exception messages can contain credentials; log only the error type.
            logger.LogError("SMTP delivery failed on {Host}:{Port} ({ErrorType}).",
                settings.Host, settings.Port, exception.GetType().Name);
            throw;
        }
    }
}
