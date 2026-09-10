using Microsoft.Extensions.Options;
using MimeKit;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Infrastructure.Email;

public sealed class SmtpPasswordResetEmailSender(IOptions<EmailSettings> settings) : IPasswordResetEmailSender
{
    // Uses the existing SMTP transport; the browser reset form belongs to the later frontend phase.
    public async Task SendAsync(PasswordResetEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.Value.FromName, settings.Value.FromAddress));
        message.To.Add(new MailboxAddress(string.Empty, email.RecipientEmail));
        message.Subject = "Reset your OpsDesk password";
        message.Body = new TextPart("plain")
        {
            Text = $"A password reset was requested for your OpsDesk account.\n\n" +
                $"Reset token: {email.RawToken}\nExpires at {email.ExpiresAtUtc:O} (UTC).\n\n" +
                "Use this token with your new password. Do not share it. " +
                "If you did not request this, you can ignore this email."
        };
        await SmtpEmailTransport.SendAsync(settings.Value, message, cancellationToken);
    }
}
