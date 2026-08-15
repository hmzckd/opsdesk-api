using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpsDesk.Application.Auth.DTOs;

namespace OpsDesk.Tests.Integration;

public sealed class AuthIntegrationTests :
    IClassFixture<OpsDeskApiFactory>
{
    private readonly OpsDeskApiFactory _factory;

    public AuthIntegrationTests(OpsDeskApiFactory factory)
    {
        _factory = factory;
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