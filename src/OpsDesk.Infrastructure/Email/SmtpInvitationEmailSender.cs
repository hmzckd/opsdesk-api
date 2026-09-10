using Microsoft.Extensions.Options;
using MimeKit;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Infrastructure.Email;

public sealed class SmtpInvitationEmailSender(IOptions<EmailSettings> settings)
    : IInvitationEmailSender
{
    // Sends an invitation token using the shared SMTP connection settings.
    public async Task SendAsync(InvitationEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.Value.FromName, settings.Value.FromAddress));
        message.To.Add(new MailboxAddress(string.Empty, email.RecipientEmail));
        message.Subject = "You are invited to OpsDesk";
        message.Body = new TextPart("plain")
        {
            Text = $"You have been invited to OpsDesk as {email.Role}.\n\n" +
                $"Invitation token: {email.RawToken}\n\n" +
                $"Expires at {email.ExpiresAtUtc:O} (UTC).\n" +
                "Use this token to accept your invitation. Do not share it."
        };
        await SmtpEmailTransport.SendAsync(settings.Value, message, cancellationToken);
    }
}
