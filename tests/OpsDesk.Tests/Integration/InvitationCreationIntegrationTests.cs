using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class InvitationCreationIntegrationTests(OpsDeskApiFixture fixture)
{
    // Exercises invitation creation through the admin HTTP endpoint.
    [Fact]
    public async Task Admin_should_create_invitation_without_exposing_token()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        await LoginAsAdminAsync(client);
        string email = $"invite-{Guid.NewGuid():N}@example.com";

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/admin/invitations", new { email, role = "agent" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(email, body.RootElement.GetProperty("email").GetString());
        Assert.Equal("agent", body.RootElement.GetProperty("role").GetString());
        Assert.False(body.RootElement.TryGetProperty("token", out _));
        Assert.False(body.RootElement.TryGetProperty("tokenHash", out _));
        var message = Assert.Single(fixture.Factory.SentInvitations,
            item => item.RecipientEmail == email);
        Assert.StartsWith("inv_", message.RawToken);
        Assert.Equal(body.RootElement.GetProperty("expiresAtUtc").GetDateTime(), message.ExpiresAtUtc);
        Assert.Equal(TimeSpan.FromHours(24), message.ExpiresAtUtc -
            body.RootElement.GetProperty("createdAtUtc").GetDateTime());
        Assert.DoesNotContain(message.RawToken, body.RootElement.GetRawText());

        using HttpResponseMessage wrongPurpose = await client.PostAsJsonAsync(
            "/auth/email-verification/confirm", new { token = message.RawToken });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPurpose.StatusCode);
    }

    // Only administrators may create invitations, regardless of the request body.
    [Fact]
    public async Task Anonymous_and_customer_requests_should_be_rejected()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        var request = new { email = $"target-{Guid.NewGuid():N}@example.com", role = "agent" };
        using HttpResponseMessage anonymous = await client.PostAsJsonAsync("/admin/invitations", request);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Test", "Customer", $"customer-{Guid.NewGuid():N}@example.com", "ValidPass!"));
        AuthResponse? customer = await registration.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(customer);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customer.AccessToken);
        using HttpResponseMessage forbidden = await client.PostAsJsonAsync("/admin/invitations", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.DoesNotContain(fixture.Factory.SentInvitations, item => item.RecipientEmail == request.email);
    }

    // Neither a pending invitation nor an existing account can be replaced by a new request.
    [Fact]
    public async Task Duplicate_and_existing_account_should_return_conflict()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        await LoginAsAdminAsync(client);
        string email = $"duplicate-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage first = await client.PostAsJsonAsync(
            "/admin/invitations", new { email, role = "customer" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using HttpResponseMessage duplicate = await client.PostAsJsonAsync(
            "/admin/invitations", new { email = email.ToUpperInvariant(), role = "agent" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Single(fixture.Factory.SentInvitations, item => item.RecipientEmail == email);

        using HttpResponseMessage existing = await client.PostAsJsonAsync(
            "/admin/invitations", new { email = OpsDeskApiFactory.AdminEmail, role = "agent" });
        Assert.Equal(HttpStatusCode.Conflict, existing.StatusCode);
    }

    // Concurrent requests must not deliver two active invitations for one address.
    [Fact]
    public async Task Concurrent_requests_should_create_one_invitation()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        await LoginAsAdminAsync(client);
        string email = $"concurrent-{Guid.NewGuid():N}@example.com";
        var request = new { email, role = "agent" };
        HttpResponseMessage[] responses = await Task.WhenAll(
            client.PostAsJsonAsync("/admin/invitations", request),
            client.PostAsJsonAsync("/admin/invitations", request));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Single(fixture.Factory.SentInvitations, item => item.RecipientEmail == email);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses) response.Dispose();
        }
    }

    // Invalid role selections cannot produce a stored or delivered invitation.
    [Theory]
    [InlineData("admin")]
    [InlineData("unknown")]
    public async Task Unsupported_role_should_return_bad_request(string role)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        await LoginAsAdminAsync(client);
        string email = $"role-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/admin/invitations", new { email, role });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(fixture.Factory.SentInvitations, item => item.RecipientEmail == email);
    }

    // A controlled clock verifies replacement exactly at expiry without waiting a day.
    [Fact]
    public async Task Expired_invitation_can_be_replaced()
    {
        var clock = new InvitationTestClock(DateTimeOffset.UtcNow);
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using HttpClient client = factory.CreateClient();
        await LoginAsAdminAsync(client);
        string email = $"expired-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage first = await client.PostAsJsonAsync(
            "/admin/invitations", new { email, role = "agent" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        clock.Now = clock.Now.AddHours(24);
        using HttpResponseMessage second = await client.PostAsJsonAsync(
            "/admin/invitations", new { email, role = "agent" });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        InvitationEmail[] messages = fixture.Factory.SentInvitations.Where(item => item.RecipientEmail == email).ToArray();
        Assert.Equal(2, messages.Length);
        Assert.NotEqual(messages[0].RawToken, messages[1].RawToken);
    }

    // Failed SMTP delivery releases the invitation so an administrator can retry.
    [Fact]
    public async Task Failed_delivery_should_allow_retry()
    {
        using var failingFactory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IInvitationEmailSender>();
            services.AddSingleton<IInvitationEmailSender, FailingInvitationSender>();
        }));
        string email = $"failure-{Guid.NewGuid():N}@example.com";
        using HttpClient failingClient = failingFactory.CreateClient();
        await LoginAsAdminAsync(failingClient);
        using HttpResponseMessage failed = await failingClient.PostAsJsonAsync(
            "/admin/invitations", new { email, role = "agent" });
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

        using HttpClient client = fixture.Factory.CreateClient();
        await LoginAsAdminAsync(client);
        using HttpResponseMessage retry = await client.PostAsJsonAsync(
            "/admin/invitations", new { email, role = "agent" });
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    private sealed class InvitationTestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        // Supplies deterministic application time without affecting JWT validation clocks.
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FailingInvitationSender : IInvitationEmailSender
    {
        // Simulates a transport failure to exercise the public endpoint's recovery behavior.
        public Task SendAsync(InvitationEmail email, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated SMTP failure.");
    }

    // Uses the normal login endpoint to acquire the seeded administrator's JWT.
    private static async Task LoginAsAdminAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(
                OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuthResponse? account = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(account);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", account.AccessToken);
    }
}
