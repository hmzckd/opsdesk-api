using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Domain.Entities;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class InvitationAcceptanceIntegrationTests(OpsDeskApiFixture fixture)
{
    // An anonymous recipient accepts once, then logs in with the invited role and verified access.
    [Theory]
    [InlineData("agent", "Agent")]
    [InlineData("customer", "Customer")]
    public async Task Accept_should_create_verified_account_and_reject_replay(string role, string loginRole)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"accept-{Guid.NewGuid():N}@example.com";
        string token = await InviteAsync(client, email, role);
        var request = new { token, firstName = "Invited", lastName = "Agent", password = "ValidPass!" };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/invitations/accept", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(email, body.RootElement.GetProperty("email").GetString());
        Assert.Equal(role, body.RootElement.GetProperty("role").GetString());
        Assert.False(body.RootElement.TryGetProperty("passwordHash", out _));
        Assert.False(body.RootElement.TryGetProperty("accessToken", out _));

        using HttpResponseMessage replay = await client.PostAsJsonAsync("/auth/invitations/accept", request);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse? account = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(account);
        Assert.Equal(loginRole, account.Role);
        Assert.Equal(body.RootElement.GetProperty("userId").GetGuid(), account.UserId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
    }

    // Invalid input must leave the invitation usable; extra identity fields cannot override its scope.
    [Theory]
    [InlineData("Invited", "Person", "short")]
    [InlineData(" ", "Person", "ValidPass!")]
    [InlineData("Invited", " ", "ValidPass!")]
    public async Task Invalid_input_should_allow_corrected_retry(string firstName, string lastName, string password)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"retry-{Guid.NewGuid():N}@example.com";
        string token = await InviteAsync(client, email, "customer");
        using HttpResponseMessage invalid = await client.PostAsJsonAsync("/auth/invitations/accept",
            new { token, firstName, lastName, password });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using HttpResponseMessage corrected = await client.PostAsJsonAsync("/auth/invitations/accept",
            new { token, firstName = "Invited", lastName = "Person", password = "ValidPass!",
                email = "attacker@example.com", role = "admin" });
        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await corrected.Content.ReadAsStringAsync());
        Assert.Equal(email, body.RootElement.GetProperty("email").GetString());
        Assert.Equal("customer", body.RootElement.GetProperty("role").GetString());
    }

    // Two simultaneous requests must create only one account and consume the token once.
    [Fact]
    public async Task Concurrent_acceptance_should_have_one_winner()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"race-{Guid.NewGuid():N}@example.com";
        string token = await InviteAsync(client, email);
        var request = new { token, firstName = "Invited", lastName = "Person", password = "ValidPass!" };
        HttpResponseMessage[] responses = await Task.WhenAll(
            client.PostAsJsonAsync("/auth/invitations/accept", request),
            client.PostAsJsonAsync("/auth/invitations/accept", request));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
            using HttpResponseMessage login = await client.PostAsJsonAsync(
                "/auth/login", new LoginRequest(email, "ValidPass!"));
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses) response.Dispose();
        }
    }

    // A conflicting public registration cannot have its password or role overwritten by acceptance.
    [Fact]
    public async Task Existing_account_should_remain_unchanged_and_acceptance_should_roll_back()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"existing-{Guid.NewGuid():N}@example.com";
        string token = await InviteAsync(client, email);
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Original", "Person", email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var request = new { token, firstName = "Invited", lastName = "Person", password = "ChangedPass!" };
        using HttpResponseMessage first = await client.PostAsJsonAsync("/auth/invitations/accept", request);
        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
        using HttpResponseMessage retry = await client.PostAsJsonAsync("/auth/invitations/accept", request);
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);

        using HttpResponseMessage login = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse? account = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(account);
        Assert.Equal("Customer", account.Role);
        Assert.Equal("Original", account.FirstName);
        using HttpResponseMessage changedPassword = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(email, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.Unauthorized, changedPassword.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.Forbidden, tickets.StatusCode);
    }

    // An email-verification token must not act as an invitation, even when it has a valid format.
    [Fact]
    public async Task Verification_and_unknown_tokens_should_not_accept_invitation()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"purpose-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Test", "Person", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        string verificationToken = Assert.Single(fixture.Factory.SentEmails,
            message => message.RecipientEmail == email).RawToken;
        string[] invalidTokens = [verificationToken, "inv_" + verificationToken, "inv_invalid", ""];
        foreach (string token in invalidTokens)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/invitations/accept",
                new { token, firstName = "Invited", lastName = "Person", password = "ValidPass!" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using HttpResponseMessage confirm = await client.PostAsJsonAsync("/auth/email-verification/confirm",
            new { token = verificationToken });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
    }

    // Moving the application clock verifies exact expiry and prevents an old token replacing a fresh one.
    [Fact]
    public async Task Expired_and_replaced_tokens_should_be_rejected_but_new_token_should_work()
    {
        var clock = new AcceptanceTestClock(DateTimeOffset.UtcNow);
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using HttpClient client = factory.CreateClient();
        string email = $"expiry-{Guid.NewGuid():N}@example.com";
        string oldToken = await InviteAsync(client, email);
        clock.Now = clock.Now.AddHours(24);
        var oldRequest = new { token = oldToken, firstName = "Invited", lastName = "Person", password = "ValidPass!" };
        using HttpResponseMessage expired = await client.PostAsJsonAsync("/auth/invitations/accept", oldRequest);
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        string newToken = await InviteAsync(client, email);
        using HttpResponseMessage retired = await client.PostAsJsonAsync("/auth/invitations/accept", oldRequest);
        Assert.Equal(HttpStatusCode.BadRequest, retired.StatusCode);
        using HttpResponseMessage accepted = await client.PostAsJsonAsync("/auth/invitations/accept",
            new { token = newToken, firstName = "Invited", lastName = "Person", password = "ValidPass!" });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task Acceptance_waiting_on_a_lock_should_recheck_expiration()
    {
        var clock = new AcceptanceTestClock(DateTimeOffset.UtcNow);
        using var factory = fixture.Factory.WithWebHostBuilder(
            builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            }));
        using HttpClient client = factory.CreateClient();
        string email = $"lock-expiry-{Guid.NewGuid():N}@example.com";
        await InviteAsync(client, email);

        await using AsyncServiceScope lockScope =
            factory.Services.CreateAsyncScope();
        OpsDeskDbContext lockDatabase = lockScope.ServiceProvider
            .GetRequiredService<OpsDeskDbContext>();
        UserInvitation invitation = await lockDatabase.UserInvitations
            .SingleAsync(item => item.Email == email);
        await using var lockTransaction =
            await lockDatabase.Database.BeginTransactionAsync();
        await lockDatabase.UserInvitations
            .FromSqlInterpolated(
                $"SELECT * FROM user_invitations WHERE id = {invitation.Id} FOR UPDATE")
            .LoadAsync();

        await using AsyncServiceScope acceptScope =
            factory.Services.CreateAsyncScope();
        IInvitationRepository repository = acceptScope.ServiceProvider
            .GetRequiredService<IInvitationRepository>();
        DateTime acceptedAtUtc = clock.GetUtcNow().UtcDateTime;
        var user = new User
        {
            FirstName = "Lock",
            LastName = "Expiry",
            Email = invitation.Email,
            Role = invitation.Role,
            PasswordHash = "not-a-real-password-hash",
            CreatedAtUtc = acceptedAtUtc
        };
        user.MarkEmailVerified(acceptedAtUtc);
        Task<bool> acceptance = repository.TryAcceptAsync(
            invitation.Id, user, acceptedAtUtc);

        await WaitUntilInvitationUpdateIsBlockedAsync(factory.Services);
        clock.Now = clock.Now.AddHours(24);
        await lockTransaction.CommitAsync();

        Assert.False(await acceptance);
    }

    private static async Task WaitUntilInvitationUpdateIsBlockedAsync(
        IServiceProvider services)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            await using AsyncServiceScope scope =
                services.CreateAsyncScope();
            OpsDeskDbContext database = scope.ServiceProvider
                .GetRequiredService<OpsDeskDbContext>();
            bool blocked = await database.Database.SqlQueryRaw<bool>(
                    "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE 'SELECT * FROM user_invitations%FOR UPDATE%') AS \"Value\"")
                .SingleAsync(timeout.Token);
            if (blocked)
            {
                return;
            }

            await Task.Yield();
        }
    }

    private sealed class AcceptanceTestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        // Supplies test-controlled UTC time without sleeping or changing the system clock.
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Creates an invitation through the real admin endpoint and returns the latest captured email token.
    private async Task<string> InviteAsync(HttpClient client, string email, string role = "agent")
    {
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse? admin = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        using HttpResponseMessage invitation = await client.PostAsJsonAsync(
            "/admin/invitations", new { email, role });
        Assert.Equal(HttpStatusCode.Created, invitation.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        return fixture.Factory.SentInvitations.Last(
            message => message.RecipientEmail == email).RawToken;
    }
}
