using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Services;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuthIntegrationTests
{
    private readonly OpsDeskApiFactory _factory;

    public AuthIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    // Registration must deliver a verification token to the registered address.
    public async Task Register_should_send_verification_email()
    {
        using HttpClient client = _factory.CreateClient();
        string email = CreateEmail();

        await RegisterAsync(client, email);

        var message = Assert.Single(
            _factory.SentEmails, item => item.RecipientEmail == email);
        Assert.False(string.IsNullOrWhiteSpace(message.RawToken));
        Assert.True(message.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    // A delivered token can verify its account exactly once through HTTP.
    public async Task Confirmation_should_accept_delivered_token_and_reject_replay()
    {
        using HttpClient client = _factory.CreateClient();
        string email = CreateEmail();
        await RegisterAsync(client, email);
        var message = Assert.Single(
            _factory.SentEmails, item => item.RecipientEmail == email);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/email-verification/confirm",
            new { token = message.RawToken });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using HttpResponseMessage replay = await client.PostAsJsonAsync(
            "/auth/email-verification/confirm",
            new { token = message.RawToken });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task Resend_should_hide_unknown_accounts_and_respect_cooldown()
    {
        using HttpClient client = _factory.CreateClient();
        string email = CreateEmail();
        await RegisterAsync(client, email);

        using HttpResponseMessage known = await client.PostAsJsonAsync(
            "/auth/email-verification/resend", new { email });
        using HttpResponseMessage unknown = await client.PostAsJsonAsync(
            "/auth/email-verification/resend", new { email = CreateEmail() });

        Assert.Equal(HttpStatusCode.NoContent, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Single(_factory.SentEmails, item => item.RecipientEmail == email);
    }

    [Fact]
    public async Task Tickets_should_require_verification_and_accept_same_token_after_confirmation()
    {
        using HttpClient client = _factory.CreateClient();
        string email = CreateEmail();
        AuthResponse account = await RegisterAsync(client, email);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", account.AccessToken);

        using HttpResponseMessage me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using HttpResponseMessage denied = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var message = Assert.Single(
            _factory.SentEmails, item => item.RecipientEmail == email);
        using HttpResponseMessage confirmation = await client.PostAsJsonAsync(
            "/auth/email-verification/confirm", new { token = message.RawToken });
        Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);
        using HttpResponseMessage allowed = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Register_login_and_me_should_work()
    {
        using HttpClient client = _factory.CreateClient();
        string email = CreateEmail();

        await RegisterAsync(client, email);

        HttpResponseMessage loginResponse =
            await client.PostAsJsonAsync(
                "/auth/login",
                new LoginRequest(email, "ValidPass!"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        AuthResponse login = await ReadAuthResponse(loginResponse);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login.AccessToken);

        HttpResponseMessage meResponse =
            await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        MeResponse? me =
            await meResponse.Content
                .ReadFromJsonAsync<MeResponse>();

        Assert.NotNull(me);
        Assert.Equal(email, me.Email);
        Assert.Equal("Customer", me.Role);
    }

    [Fact]
    public async Task Duplicate_email_should_return_conflict()
    {
        using HttpClient client = _factory.CreateClient();
        string email = CreateEmail();

        await RegisterAsync(client, email);

        HttpResponseMessage duplicateResponse =
            await client.PostAsJsonAsync(
                "/auth/register",
                CreateRegisterRequest(email));

        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Overlong_first_name_should_return_bad_request()
    {
        using HttpClient client = _factory.CreateClient();

        var request = new RegisterRequest(
            new string('A', UserInputNormalizer.MaximumNameLength + 1),
            "Customer",
            CreateEmail(),
            "ValidPass!");

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/auth/register",
                request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Invalid_password_should_return_unauthorized()
    {
        using HttpClient client = _factory.CreateClient();
        string email = CreateEmail();

        await RegisterAsync(client, email);

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/auth/login",
                new LoginRequest(email, "WrongPassword!"));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Me_without_token_should_return_unauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response =
            await client.GetAsync("/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Customer_should_be_forbidden_from_admin()
    {
        using HttpClient client = _factory.CreateClient();
        AuthResponse customer =
            await RegisterAsync(client, CreateEmail());

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                customer.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync("/admin/access");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Seeded_admin_should_access_admin_endpoint()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage loginResponse =
            await client.PostAsJsonAsync(
                "/auth/login",
                new LoginRequest(
                    OpsDeskApiFactory.AdminEmail,
                    OpsDeskApiFactory.AdminPassword));

        AuthResponse admin =
            await ReadAuthResponse(loginResponse);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                admin.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync("/admin/access");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<AuthResponse> RegisterAsync(
        HttpClient client,
        string email)
    {
        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/auth/register",
                CreateRegisterRequest(email));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadAuthResponse(response);
    }

    private static RegisterRequest CreateRegisterRequest(
        string email)
    {
        return new RegisterRequest(
            "Test",
            "Customer",
            email,
            "ValidPass!");
    }

    private static async Task<AuthResponse> ReadAuthResponse(
        HttpResponseMessage response)
    {
        AuthResponse? result =
            await response.Content
                .ReadFromJsonAsync<AuthResponse>();

        return result
            ?? throw new InvalidOperationException(
                "Auth response body was empty.");
    }

    private static string CreateEmail()
    {
        return $"user-{Guid.NewGuid():N}@example.com";
    }
}
