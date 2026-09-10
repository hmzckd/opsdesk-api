using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Infrastructure.Email;

namespace OpsDesk.Tests.Integration;

public sealed class SmtpEmailVerificationEmailSenderTests :
    IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private readonly IContainer _mailpitContainer =
        new ContainerBuilder("axllent/mailpit:v1.30.0")
            .WithPortBinding(SmtpPort, true)
            .WithPortBinding(HttpPort, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(
                        request => request
                            .ForPort(HttpPort)
                            .ForPath("/readyz")))
            .Build();

    public Task InitializeAsync()
    {
        return _mailpitContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _mailpitContainer.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_should_deliver_verification_email_to_mailpit()
    {
        const string rawToken = "email-verification-token";
        var settings = new EmailSettings
        {
            Host = _mailpitContainer.Hostname,
            Port = _mailpitContainer.GetMappedPublicPort(SmtpPort),
            UseSsl = false,
            FromAddress = "noreply@opsdesk.local",
            FromName = "OpsDesk",
            VerificationUrl =
                "http://localhost:5044/auth/email-verification/confirm"
        };
        IEmailVerificationEmailSender sender =
            new SmtpEmailVerificationEmailSender(
                Options.Create(settings));
        var email = new EmailVerificationEmail(
            "customer@example.com",
            rawToken,
            new DateTime(
                2026,
                9,
                4,
                20,
                0,
                0,
                DateTimeKind.Utc));

        await sender.SendAsync(email);

        using var client = new HttpClient
        {
            BaseAddress = new Uri(
                $"http://{_mailpitContainer.Hostname}:" +
                _mailpitContainer.GetMappedPublicPort(HttpPort))
        };
        MailpitMessagesResponse? response =
            await client.GetFromJsonAsync<MailpitMessagesResponse>(
                "/api/v1/messages",
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        MailpitMessageSummary message =
            Assert.Single(
                Assert.IsType<MailpitMessagesResponse>(response)
                    .Messages);
        MailpitAddress recipient = Assert.Single(message.To);
        string htmlBody = await client.GetStringAsync(
            "/view/latest.html");

        Assert.Equal(
            "Verify your OpsDesk email",
            message.Subject);
        Assert.Equal(
            "customer@example.com",
            recipient.Address);
        Assert.Contains(
            settings.VerificationUrl + "?token=" + rawToken,
            htmlBody,
            StringComparison.Ordinal);
    }

    private sealed record MailpitMessagesResponse(
        IReadOnlyList<MailpitMessageSummary> Messages);

    // Verifies the invitation adapter really delivers its distinct content over SMTP.
    [Fact]
    public async Task SendAsync_should_deliver_invitation_email_to_mailpit()
    {
        var settings = new EmailSettings
        {
            Host = _mailpitContainer.Hostname,
            Port = _mailpitContainer.GetMappedPublicPort(SmtpPort),
            FromAddress = "noreply@opsdesk.local",
            FromName = "OpsDesk"
        };
        var sender = new SmtpInvitationEmailSender(Options.Create(settings));
        var email = new OpsDesk.Application.Invitations.Models.InvitationEmail(
            "invited@example.com", OpsDesk.Domain.Enums.UserRole.Agent,
            "inv_test-token", DateTime.UtcNow.AddHours(24));

        await sender.SendAsync(email);

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{_mailpitContainer.Hostname}:" +
                _mailpitContainer.GetMappedPublicPort(HttpPort))
        };
        var response = await client.GetFromJsonAsync<MailpitMessagesResponse>("/api/v1/messages");
        var message = Assert.Single(Assert.IsType<MailpitMessagesResponse>(response).Messages);
        Assert.Equal("You are invited to OpsDesk", message.Subject);
        Assert.Equal(email.RecipientEmail, Assert.Single(message.To).Address);
        string text = await client.GetStringAsync("/view/latest.txt");
        Assert.Contains(email.RawToken, text);
        Assert.Contains("Agent", text);
    }

    private sealed record MailpitMessageSummary(
        string Subject,
        IReadOnlyList<MailpitAddress> To);

    // Verifies the password-reset adapter over a real SMTP connection, including its distinct purpose.
    [Fact]
    public async Task SendAsync_should_deliver_password_reset_email_to_mailpit()
    {
        var settings = new EmailSettings
        {
            Host = _mailpitContainer.Hostname,
            Port = _mailpitContainer.GetMappedPublicPort(SmtpPort),
            FromAddress = "noreply@opsdesk.local", FromName = "OpsDesk"
        };
        var sender = new SmtpPasswordResetEmailSender(Options.Create(settings));
        var email = new PasswordResetEmail("recovery@example.com", "pwd_test-token", DateTime.UtcNow.AddMinutes(30));
        await sender.SendAsync(email);
        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{_mailpitContainer.Hostname}:" +
                _mailpitContainer.GetMappedPublicPort(HttpPort))
        };
        var response = await client.GetFromJsonAsync<MailpitMessagesResponse>("/api/v1/messages");
        var message = Assert.Single(Assert.IsType<MailpitMessagesResponse>(response).Messages);
        Assert.Equal("Reset your OpsDesk password", message.Subject);
        Assert.Equal(email.RecipientEmail, Assert.Single(message.To).Address);
        string text = await client.GetStringAsync("/view/latest.txt");
        Assert.Contains(email.RawToken, text);
        Assert.Contains("If you did not request this", text);
    }

    private sealed record MailpitAddress(string Address);
}
