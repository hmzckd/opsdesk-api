using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace OpsDesk.Infrastructure.Email;

internal static class SmtpEmailTransport
{
    // Disposes the connection without an extra QUIT exchange that could invalidate successful delivery.
    internal static async Task SendAsync(
        EmailSettings settings, MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port,
            settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None,
            cancellationToken);
        await client.SendAsync(message, cancellationToken);
    }
}
