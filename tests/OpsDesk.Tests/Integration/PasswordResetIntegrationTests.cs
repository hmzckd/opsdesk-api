using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Domain.Entities;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class PasswordResetIntegrationTests(OpsDeskApiFixture fixture)
{
    // Failure after SQL writes but before transaction commit must roll back all three reset effects.
    [Fact]
    public async Task Failed_transaction_should_preserve_password_session_and_token_for_retry()
    {
        var failure = new FailResetSaveOnce();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" }));
            builder.ConfigureServices(services => services.ConfigureDbContext<OpsDeskDbContext>(
                options => options.AddInterceptors(failure)));
        });
        using HttpClient client = factory.CreateClient();
        var account = await RegisterAndRequestAsync(client);
        failure.UserId = account.Session.UserId;
        using HttpResponseMessage failed = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using HttpResponseMessage oldLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(account.Email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.OK, oldLogin.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.Session.AccessToken);
        using HttpResponseMessage me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using HttpResponseMessage retried = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.NoContent, retried.StatusCode);
    }

    private sealed class FailResetSaveOnce : SaveChangesInterceptor
    {
        private int _armed = 1;
        public Guid UserId { get; set; }

        // Injects one infrastructure failure after EF executed SQL, without replacing the real repository.
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            bool resettingTarget = eventData.Context?.ChangeTracker.Entries<User>()
                .Any(entry => entry.Entity.Id == UserId && entry.Entity.AuthVersion > 0) == true;
            if (resettingTarget && Interlocked.Exchange(ref _armed, 0) == 1)
                throw new IOException("Simulated failure before reset transaction commit.");
            return ValueTask.FromResult(result);
        }
    }

    // Verification credentials and well-formed but unknown reset credentials cannot replace a password.
    [Fact]
    public async Task Wrong_purpose_and_unknown_tokens_should_preserve_account_access()
    {
        using var factory = CreateRecoveryFactory();
        using HttpClient client = factory.CreateClient();
        var account = await RegisterAndRequestAsync(client);
        EmailVerificationEmail verification = Assert.Single(fixture.Factory.SentEmails, message => message.RecipientEmail == account.Email);
        foreach (string token in new[] { verification.RawToken, "inv_" + new string('A', 43), "pwd_" + new string('A', 43), "bad-token" })
        {
            using HttpResponseMessage rejected = await client.PostAsJsonAsync("/auth/reset-password",
                new ResetPasswordRequest(token, "ChangedPass!"));
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        using HttpResponseMessage confirmed = await client.PostAsJsonAsync("/auth/email-verification/confirm",
            new ConfirmEmailRequest(verification.RawToken));
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        using HttpResponseMessage reset = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(account.Email, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse current = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        Assert.Equal(account.Session.Role, current.Role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
    }

    // Reset limits are configurable and separate from the forgot-password request budget.
    [Fact]
    public async Task Reset_should_enforce_its_own_configured_ip_limit()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:RateLimits:ResetPermitLimit"] = "2" })));
        using HttpClient client = factory.CreateClient();
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordRequest("bad-token", "ChangedPass!"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using HttpResponseMessage limited = await client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordRequest("bad-token", "ChangedPass!"));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email = $"unknown-{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
    }

    // The expiry boundary is exclusive; advancing a test clock avoids a thirty-minute wait.
    [Theory]
    [InlineData(1799, HttpStatusCode.NoContent)]
    [InlineData(1800, HttpStatusCode.BadRequest)]
    public async Task Reset_should_enforce_the_exact_expiration_boundary(int elapsedSeconds, HttpStatusCode expected)
    {
        var clock = new ResetClock(DateTimeOffset.UtcNow.AddMinutes(1));
        using var factory = CreateRecoveryFactory(clock);
        using HttpClient client = factory.CreateClient();
        var account = await RegisterAndRequestAsync(client);
        clock.Advance(TimeSpan.FromSeconds(elapsedSeconds));
        using HttpResponseMessage reset = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, "ChangedPass!"));
        Assert.Equal(expected, reset.StatusCode);
        string expectedPassword = expected == HttpStatusCode.NoContent ? "ChangedPass!" : "OriginalPass!";
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(account.Email, expectedPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    // A newly delivered credential replaces the previous one without prematurely changing the password.
    [Fact]
    public async Task Replacement_token_should_reject_the_previous_token_and_allow_the_new_one()
    {
        var clock = new ResetClock(DateTimeOffset.UtcNow.AddMinutes(1));
        using var factory = CreateRecoveryFactory(clock);
        using HttpClient client = factory.CreateClient();
        var account = await RegisterAndRequestAsync(client);
        clock.Advance(TimeSpan.FromSeconds(60));
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email = account.Email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        PasswordResetEmail replacement = await fixture.Factory.PasswordResetEmails.WaitForAsync(account.Email);
        using HttpResponseMessage old = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        using HttpResponseMessage current = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(replacement.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
    }

    // Competing HTTP requests cannot both consume the same reset credential.
    [Fact]
    public async Task Concurrent_resets_should_have_exactly_one_winner()
    {
        using var factory = CreateRecoveryFactory();
        using HttpClient client = factory.CreateClient();
        var account = await RegisterAndRequestAsync(client);
        string[] passwords = ["FirstChanged!", "SecondChanged!"];
        Task<HttpResponseMessage>[] attempts = passwords.Select(password => client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, password))).ToArray();
        HttpResponseMessage[] responses = await Task.WhenAll(attempts);
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
            for (int index = 0; index < passwords.Length; index++)
            {
                using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(account.Email, passwords[index]));
                Assert.Equal(responses[index].StatusCode == HttpStatusCode.NoContent ? HttpStatusCode.OK : HttpStatusCode.Unauthorized,
                    login.StatusCode);
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.Session.AccessToken);
            using HttpResponseMessage me = await client.GetAsync("/me");
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses) response.Dispose();
        }
    }

    // Each isolated host gets its own request limiter while retaining the real PostgreSQL persistence.
    private WebApplicationFactory<Program> CreateRecoveryFactory(TimeProvider? clock = null)
    {
        return fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" }));
            if (clock is not null)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(clock);
                });
            }
        });
    }

    // Prepares a real account through HTTP and observes the email at the delivery boundary.
    private async Task<(string Email, AuthResponse Session, PasswordResetEmail Message)> RegisterAndRequestAsync(HttpClient client)
    {
        string email = $"reset-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Reset", "User", email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        AuthResponse session = Assert.IsType<AuthResponse>(await registration.Content.ReadFromJsonAsync<AuthResponse>());
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        PasswordResetEmail message = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        return (email, session, message);
    }

    private sealed class ResetClock(DateTimeOffset now) : TimeProvider
    {
        private long _ticks = now.UtcTicks;

        // Keeps application time deterministic and safe to read from the background delivery worker.
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        // Moves only the test clock; no process-wide clock changes or arbitrary sleeps are needed.
        public void Advance(TimeSpan amount) => Interlocked.Add(ref _ticks, amount.Ticks);
    }

    // Validation failure preserves the credential so the user can correct the password and retry.
    [Fact]
    public async Task Invalid_new_password_should_not_consume_the_token_or_revoke_the_session()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" })));
        using HttpClient client = factory.CreateClient();
        string email = $"validation-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Reset", "User", email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        AuthResponse original = Assert.IsType<AuthResponse>(await registration.Content.ReadFromJsonAsync<AuthResponse>());
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var delivered = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        using HttpResponseMessage invalid = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(delivered.RawToken, "short"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", original.AccessToken);
        using HttpResponseMessage me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using HttpResponseMessage corrected = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(delivered.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.NoContent, corrected.StatusCode);
    }

    // A reset changes the password, consumes the credential and rejects every pre-reset access token.
    [Fact]
    public async Task Reset_should_replace_password_consume_token_and_invalidate_existing_sessions()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" })));
        using HttpClient client = factory.CreateClient();
        string email = $"reset-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Reset", "User", email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        AuthResponse original = Assert.IsType<AuthResponse>(await registration.Content.ReadFromJsonAsync<AuthResponse>());
        using HttpResponseMessage secondLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "OriginalPass!"));
        AuthResponse secondSession = Assert.IsType<AuthResponse>(await secondLogin.Content.ReadFromJsonAsync<AuthResponse>());
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var delivered = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);

        var request = new { token = delivered.RawToken, newPassword = "ChangedPass!" };
        using HttpResponseMessage reset = await client.PostAsJsonAsync("/auth/reset-password", request);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using HttpResponseMessage replay = await client.PostAsJsonAsync("/auth/reset-password", request);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using HttpResponseMessage oldLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        foreach (string accessToken in new[] { original.AccessToken, secondSession.AccessToken })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage me = await client.GetAsync("/me");
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = null;
        using HttpResponseMessage newLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        AuthResponse current = Assert.IsType<AuthResponse>(await newLogin.Content.ReadFromJsonAsync<AuthResponse>());
        Assert.Equal(original.UserId, current.UserId);
        Assert.Equal(original.Role, current.Role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        using HttpResponseMessage currentMe = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, currentMe.StatusCode);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.Forbidden, tickets.StatusCode);
    }
}
