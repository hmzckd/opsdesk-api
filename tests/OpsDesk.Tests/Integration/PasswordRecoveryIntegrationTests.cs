using System.Net;
using System.Net.Http.Json;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Application.Auth.Interfaces;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class PasswordRecoveryIntegrationTests(OpsDeskApiFixture fixture)
{
    // A later successful job confirms failure handling has finished before the clock advances for retry.
    [Fact]
    public async Task Failed_background_delivery_should_retry_without_changing_the_public_response()
    {
        string email = $"failed-{Guid.NewGuid():N}@example.com";
        string barrier = $"after-failure-{Guid.NewGuid():N}@example.com";
        var clock = new RecoveryClock(DateTimeOffset.UtcNow.AddMinutes(1));
        var sender = new FailOnceResetSender(email, fixture.Factory.PasswordResetEmails);
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                services.RemoveAll<IPasswordResetEmailSender>();
                services.AddSingleton<IPasswordResetEmailSender>(sender);
            });
        });
        using HttpClient client = factory.CreateClient();
        foreach (string address in new[] { email, barrier })
        {
            using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
                new RegisterRequest("Recovery", "User", address, "ValidPass!"));
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        }
        using HttpResponseMessage request = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        clock.Advance(TimeSpan.FromSeconds(1));
        using HttpResponseMessage barrierResponse = await client.PostAsJsonAsync("/auth/forgot-password", new { email = barrier });
        Assert.Equal(HttpStatusCode.Accepted, barrierResponse.StatusCode);
        await fixture.Factory.PasswordResetEmails.WaitForAsync(barrier);
        Assert.DoesNotContain(fixture.Factory.PasswordResetEmails.Delivered, message => message.RecipientEmail == email);
        Assert.NotNull(sender.FailedToken);
        using HttpResponseMessage revoked = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(sender.FailedToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.BadRequest, revoked.StatusCode);
        clock.Advance(TimeSpan.FromSeconds(61));
        PasswordResetEmail retry = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        Assert.NotEqual(sender.FailedToken, retry.RawToken);
    }

    private sealed class FailOnceResetSender(string recipient, PasswordResetMailbox mailbox) : IPasswordResetEmailSender
    {
        private int _attempts;
        public string? FailedToken { get; private set; }

        // Simulates one transport failure while using the same capture adapter for subsequent delivery.
        public Task SendAsync(PasswordResetEmail email, CancellationToken cancellationToken = default)
        {
            if (email.RecipientEmail == recipient && Interlocked.Increment(ref _attempts) == 1)
            {
                FailedToken = email.RawToken;
                throw new IOException("Simulated SMTP delivery failure.");
            }
            return mailbox.SendAsync(email, cancellationToken);
        }
    }

    // A new host resumes a request that was accepted while its delivery worker was disabled.
    [Fact]
    public async Task Queued_request_should_survive_host_restart()
    {
        string email = $"durable-{Guid.NewGuid():N}@example.com";
        using (var firstFactory = fixture.Factory.WithWebHostBuilder(_ => { }))
        using (HttpClient client = firstFactory.CreateClient())
        {
            using HttpResponseMessage registered = await client.PostAsJsonAsync("/auth/register",
                new RegisterRequest("Recovery", "User", email, "ValidPass!"));
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
            using HttpResponseMessage accepted = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }
        using var restarted = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" })));
        using HttpClient restartedClient = restarted.CreateClient();
        PasswordResetEmail delivered = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        Assert.Equal(email, delivered.RecipientEmail);
    }

    // A later delivery acts as a queue barrier, so absence of a duplicate does not depend on sleeping.
    [Fact]
    public async Task Account_cooldown_should_suppress_duplicates_and_allow_delivery_after_sixty_seconds()
    {
        var clock = new RecoveryClock(DateTimeOffset.UtcNow.AddMinutes(1));
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            });
        });
        using HttpClient client = factory.CreateClient();
        string email = $"cooldown-{Guid.NewGuid():N}@example.com";
        string barrier = $"barrier-{Guid.NewGuid():N}@example.com";
        foreach (string address in new[] { email, barrier })
        {
            using HttpResponseMessage registered = await client.PostAsJsonAsync("/auth/register",
                new RegisterRequest("Recovery", "User", address, "ValidPass!"));
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }
        using HttpResponseMessage first = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        PasswordResetEmail firstEmail = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        clock.Advance(TimeSpan.FromSeconds(10));
        using HttpResponseMessage duplicate = await client.PostAsJsonAsync("/auth/forgot-password",
            new { email = email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        clock.Advance(TimeSpan.FromSeconds(1));
        using HttpResponseMessage barrierResponse = await client.PostAsJsonAsync("/auth/forgot-password", new { email = barrier });
        Assert.Equal(HttpStatusCode.Accepted, barrierResponse.StatusCode);
        await fixture.Factory.PasswordResetEmails.WaitForAsync(barrier);
        Assert.Single(fixture.Factory.PasswordResetEmails.Delivered, message => message.RecipientEmail == email);
        clock.Advance(TimeSpan.FromSeconds(49));
        using HttpResponseMessage retry = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        PasswordResetEmail replacement = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        Assert.NotEqual(firstEmail.RawToken, replacement.RawToken);
    }

    // Even a sender that has not completed cannot hold the public recovery response open.
    [Fact]
    public async Task Slow_delivery_should_not_delay_the_http_acknowledgement()
    {
        string email = $"slow-{Guid.NewGuid():N}@example.com";
        var sender = new BlockingResetSender(email);
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPasswordResetEmailSender>();
                services.AddSingleton<IPasswordResetEmailSender>(sender);
            });
        });
        using HttpClient client = factory.CreateClient();
        try
        {
            using HttpResponseMessage registered = await client.PostAsJsonAsync("/auth/register",
                new RegisterRequest("Recovery", "User", email, "ValidPass!"));
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
            using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/forgot-password", new { email })
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            await sender.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.False(sender.Release.Task.IsCompleted);
        }
        finally
        {
            sender.Release.TrySetResult(true);
        }
    }

    private sealed class RecoveryClock(DateTimeOffset now) : TimeProvider
    {
        private long _ticks = now.UtcTicks;

        // Reads the application clock safely while a background worker is also using it.
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        // Advances deterministic time without delaying the test or changing the system clock.
        public void Advance(TimeSpan amount) => Interlocked.Add(ref _ticks, amount.Ticks);
    }

    private sealed class BlockingResetSender(string recipient) : IPasswordResetEmailSender
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Blocks only the test recipient until the test releases it; other queued jobs are irrelevant.
        public async Task SendAsync(PasswordResetEmail email, CancellationToken cancellationToken = default)
        {
            if (email.RecipientEmail != recipient) return;
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    // The configured per-IP limit is independent of whether the requested account exists.
    [Fact]
    public async Task Forgot_password_should_enforce_configured_ip_limit()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:RateLimits:RequestPermitLimit"] = "2" })));
        using HttpClient client = factory.CreateClient();
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using HttpResponseMessage accepted = await client.PostAsJsonAsync("/auth/forgot-password",
                new { email = $"limit-{Guid.NewGuid():N}@example.com" });
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }
        using HttpResponseMessage limited = await client.PostAsJsonAsync("/auth/forgot-password",
            new { email = $"limit-{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    // The HTTP response completes before a background worker sends a purpose-specific reset token.
    [Fact]
    public async Task Recovery_request_should_deliver_a_thirty_minute_token_in_background()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" })));
        using HttpClient client = factory.CreateClient();
        string email = $"delivery-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Recovery", "User", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        DateTime before = DateTime.UtcNow;
        using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        PasswordResetEmail message = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        Assert.StartsWith("pwd_", message.RawToken);
        Assert.Equal(47, message.RawToken.Length);
        Assert.InRange(message.ExpiresAtUtc, before.AddMinutes(30), DateTime.UtcNow.AddMinutes(30));
        Assert.DoesNotContain(message.RawToken, await response.Content.ReadAsStringAsync());
        using HttpResponseMessage wrongPurpose = await client.PostAsJsonAsync(
            "/auth/email-verification/confirm", new { token = message.RawToken });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPurpose.StatusCode);
    }

    // Both addresses receive the same response; requesting recovery must not change the password.
    [Fact]
    public async Task Forgot_password_should_accept_known_and_unknown_addresses_identically()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"recovery-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Recovery", "User", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        using HttpResponseMessage known = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        using HttpResponseMessage unknown = await client.PostAsJsonAsync("/auth/forgot-password",
            new { email = $"unknown-{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
