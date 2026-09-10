using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;
using MimeKit;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Infrastructure.Email;

public sealed class SmtpEmailVerificationEmailSender :
    IEmailVerificationEmailSender
{
    private readonly EmailSettings _settings;

    public SmtpEmailVerificationEmailSender(
        IOptions<EmailSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings.Value;
    }

    /// <summary>
    /// Builds and sends one verification email through the configured SMTP server.
    /// </summary>
    public async Task SendAsync(
        EmailVerificationEmail email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        using MimeMessage message = CreateMessage(email);
        await SmtpEmailTransport.SendAsync(_settings, message, cancellationToken);
    }

    private MimeMessage CreateMessage(
        EmailVerificationEmail email)
    {
        string verificationLink =
            CreateVerificationLink(email.RawToken);
        string expiresAtUtc = email.ExpiresAtUtc.ToString(
            "O",
            CultureInfo.InvariantCulture);
        string encodedLink =
            WebUtility.HtmlEncode(verificationLink);
        string encodedExpiration =
            WebUtility.HtmlEncode(expiresAtUtc);

        var message = new MimeMessage();
        message.From.Add(
            new MailboxAddress(
                _settings.FromName,
                _settings.FromAddress));
        message.To.Add(
            new MailboxAddress(
                string.Empty,
                email.RecipientEmail));
        message.Subject = "Verify your OpsDesk email";
        message.Body = new BodyBuilder
        {
            TextBody =
                "Verify your OpsDesk email by opening this link:" +
                Environment.NewLine +
                verificationLink +
                Environment.NewLine +
                "This link expires at " +
                expiresAtUtc +
                ".",
            HtmlBody = $$"""
                <!doctype html>
                <html lang="en">
                <body>
                  <h1>Verify your OpsDesk email</h1>
                  <p>Open the link below to verify your email address.</p>
                  <p><a href="{{encodedLink}}">Verify email</a></p>
                  <p>This link expires at {{encodedExpiration}} UTC.</p>
                </body>
                </html>
                """
        }.ToMessageBody();

        return message;
    }

    private string CreateVerificationLink(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var uriBuilder = new UriBuilder(
            _settings.VerificationUrl)
        {
            Query =
                "token=" +
                Uri.EscapeDataString(rawToken)
        };

        return uriBuilder.Uri.AbsoluteUri;
    }
}
