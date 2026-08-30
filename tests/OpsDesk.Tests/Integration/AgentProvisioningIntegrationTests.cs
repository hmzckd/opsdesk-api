using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OpsDesk.Application.Auth.DTOs;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class AgentProvisioningIntegrationTests
{
    private readonly OpsDeskApiFactory _factory;

    public AgentProvisioningIntegrationTests(
        OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies an Admin can provision an Agent that authenticates normally.
    /// </summary>
    [Fact]
    public async Task Admin_should_create_agent_that_can_log_in()
    {
        using HttpClient client = _factory.CreateClient();
        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                admin.AccessToken);

        string email = $"agent-{Guid.NewGuid():N}@example.com";

        HttpResponseMessage createResponse =
            await client.PostAsJsonAsync(
                "/admin/agents",
                CreateAgentRequest(email));

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        JsonElement responseJson =
            await createResponse.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.False(
            responseJson.TryGetProperty("password", out _));
        Assert.False(
            responseJson.TryGetProperty("passwordHash", out _));

        AgentProvisioningResponse? created =
            responseJson.Deserialize<AgentProvisioningResponse>(
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(email, created.Email);
        Assert.Equal("Agent", created.Role);

        AuthResponse agent = await LoginAsync(
            client,
            email,
            "AgentPass!");

        Assert.Equal(created.Id, agent.UserId);
        Assert.Equal("Agent", agent.Role);
    }

    /// <summary>
    /// Verifies callers cannot override the server-owned Agent role.
    /// </summary>
    [Fact]
    public async Task Requested_role_should_not_override_agent_role()
    {
        using HttpClient client = _factory.CreateClient();
        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                admin.AccessToken);

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/admin/agents",
                new
                {
                    firstName = "Role",
                    lastName = "Override",
                    email = $"agent-{Guid.NewGuid():N}@example.com",
                    password = "AgentPass!",
                    role = "Admin"
                });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        AgentProvisioningResponse? created =
            await response.Content
                .ReadFromJsonAsync<AgentProvisioningResponse>();

        Assert.NotNull(created);
        Assert.Equal("Agent", created.Role);
    }

    /// <summary>
    /// Verifies Agent provisioning requires an authenticated identity.
    /// </summary>
    [Fact]
    public async Task Anonymous_should_be_unauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/admin/agents",
                CreateAgentRequest(
                    $"agent-{Guid.NewGuid():N}@example.com"));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    /// <summary>
    /// Verifies a Customer cannot provision a privileged Agent identity.
    /// </summary>
    [Fact]
    public async Task Customer_should_be_forbidden()
    {
        using HttpClient client = _factory.CreateClient();
        AuthResponse customer = await RegisterAsync(
            client,
            $"customer-{Guid.NewGuid():N}@example.com");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                customer.AccessToken);

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/admin/agents",
                CreateAgentRequest(
                    $"agent-{Guid.NewGuid():N}@example.com"));

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    /// <summary>
    /// Verifies one email address cannot identify multiple Users.
    /// </summary>
    [Fact]
    public async Task Duplicate_email_should_return_conflict()
    {
        using HttpClient client = _factory.CreateClient();
        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                admin.AccessToken);

        string email = $"agent-{Guid.NewGuid():N}@example.com";
        AgentProvisioningRequest request =
            CreateAgentRequest(email);

        HttpResponseMessage firstResponse =
            await client.PostAsJsonAsync(
                "/admin/agents",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        HttpResponseMessage duplicateResponse =
            await client.PostAsJsonAsync(
                "/admin/agents",
                request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateResponse.StatusCode);
    }

    /// <summary>
    /// Verifies Agent provisioning reuses the public credential rules.
    /// </summary>
    [Theory]
    [InlineData("invalid-email", "AgentPass!")]
    [InlineData("invalid-password@example.com", "agentpass")]
    public async Task Invalid_credentials_should_return_bad_request(
        string email,
        string password)
    {
        using HttpClient client = _factory.CreateClient();
        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                admin.AccessToken);

        var request = new AgentProvisioningRequest(
            "Support",
            "Agent",
            email,
            password);

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/admin/agents",
                request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private static async Task<AuthResponse> RegisterAsync(
        HttpClient client,
        string email)
    {
        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/auth/register",
                new RegisterRequest(
                    "Test",
                    "Customer",
                    email,
                    "CustomerPass!"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadAuthResponseAsync(response);
    }

    private static async Task<AuthResponse> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/auth/login",
                new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadAuthResponseAsync(response);
    }

    private static async Task<AuthResponse> ReadAuthResponseAsync(
        HttpResponseMessage response)
    {
        AuthResponse? result = await response.Content
            .ReadFromJsonAsync<AuthResponse>();

        return result ?? throw new InvalidOperationException(
            "Auth response body was empty.");
    }

    private static AgentProvisioningRequest CreateAgentRequest(
        string email)
    {
        return new AgentProvisioningRequest(
            "Support",
            "Agent",
            email,
            "AgentPass!");
    }

    private sealed record AgentProvisioningResponse(
        Guid Id,
        string FirstName,
        string LastName,
        string Email,
        string Role,
        DateTime CreatedAtUtc);

    private sealed record AgentProvisioningRequest(
        string FirstName,
        string LastName,
        string Email,
        string Password);
}
