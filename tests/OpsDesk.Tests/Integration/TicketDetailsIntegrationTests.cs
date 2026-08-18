using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class TicketDetailsIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private readonly OpsDeskApiFactory _factory;

    public TicketDetailsIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies a Customer can retrieve a Ticket they requested.
    /// </summary>
    [Fact]
    public async Task Requester_should_view_own_ticket()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client);
        SetBearerToken(client, customer.AccessToken);

        TicketResponse createdTicket = await CreateTicketAsync(client);

        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{createdTicket.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketResponse? returnedTicket =
            await response.Content
                .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        Assert.NotNull(returnedTicket);
        Assert.Equal(createdTicket.Id, returnedTicket.Id);
        Assert.Equal(createdTicket.Title, returnedTicket.Title);
        Assert.Equal(createdTicket.Description, returnedTicket.Description);
        Assert.Equal(createdTicket.Priority, returnedTicket.Priority);
        Assert.Equal(createdTicket.Status, returnedTicket.Status);
        Assert.Equal(createdTicket.RequesterId, returnedTicket.RequesterId);
        Assert.Equal(createdTicket.AssigneeId, returnedTicket.AssigneeId);
        Assert.Equal(
            createdTicket.CreatedAtUtc,
            returnedTicket.CreatedAtUtc,
            TimeSpan.FromMilliseconds(1));
        Assert.Equal(
            createdTicket.UpdatedAtUtc,
            returnedTicket.UpdatedAtUtc,
            TimeSpan.FromMilliseconds(1));
        Assert.Equal(createdTicket.ResolvedAtUtc, returnedTicket.ResolvedAtUtc);
        Assert.Equal(createdTicket.ClosedAtUtc, returnedTicket.ClosedAtUtc);
    }

    /// <summary>
    /// Verifies a Customer cannot discover another requester's Ticket.
    /// </summary>
    [Fact]
    public async Task Customer_should_not_view_another_requesters_ticket()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterAsync(client);
        SetBearerToken(client, requester.AccessToken);

        TicketResponse createdTicket = await CreateTicketAsync(client);

        AuthResponse otherCustomer = await RegisterAsync(client);
        SetBearerToken(client, otherCustomer.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{createdTicket.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies support roles can retrieve any Ticket.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public async Task Support_staff_should_view_any_ticket(
        UserRole role)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterAsync(client);
        SetBearerToken(client, requester.AccessToken);

        TicketResponse createdTicket = await CreateTicketAsync(client);

        AuthResponse supportUser =
            await AuthenticateSupportUserAsync(client, role);

        SetBearerToken(client, supportUser.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{createdTicket.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketResponse? returnedTicket =
            await response.Content
                .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        Assert.NotNull(returnedTicket);
        Assert.Equal(createdTicket.Id, returnedTicket.Id);
    }

    /// <summary>
    /// Verifies an unknown Ticket identifier returns Not Found.
    /// </summary>
    [Fact]
    public async Task Authenticated_user_should_not_find_unknown_ticket()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client);
        SetBearerToken(client, customer.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies Ticket details require authentication.
    /// </summary>
    [Fact]
    public async Task Anonymous_user_should_not_view_ticket_details()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Registers a unique Customer through the public authentication API.
    /// </summary>
    private static async Task<AuthResponse> RegisterAsync(
        HttpClient client)
    {
        var request = new RegisterRequest(
            "Ticket",
            "Viewer",
            $"viewer-{Guid.NewGuid():N}@example.com",
            "ValidPass!");

        HttpResponseMessage response =
            await client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        AuthResponse? result =
            await response.Content.ReadFromJsonAsync<AuthResponse>();

        return result
            ?? throw new InvalidOperationException(
                "Auth response body was empty.");
    }

    /// <summary>
    /// Creates a Ticket through the public Ticket API.
    /// </summary>
    private static async Task<TicketResponse> CreateTicketAsync(
        HttpClient client)
    {
        var request = new CreateTicketRequest(
            "Cannot access the payroll portal",
            "The portal returns an access denied message.");

        HttpResponseMessage response =
            await client.PostAsJsonAsync("/tickets", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        TicketResponse? result =
            await response.Content
                .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        return result
            ?? throw new InvalidOperationException(
                "Ticket response body was empty.");
    }

    /// <summary>
    /// Authenticates an Agent or the seeded test Admin.
    /// </summary>
    private async Task<AuthResponse> AuthenticateSupportUserAsync(
        HttpClient client,
        UserRole role)
    {
        return role switch
        {
            UserRole.Agent => await CreateAgentAsync(client),
            UserRole.Admin => await LoginAsync(
                client,
                OpsDeskApiFactory.AdminEmail,
                OpsDeskApiFactory.AdminPassword),
            _ => throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "Only support roles are accepted by this test.")
        };
    }

    /// <summary>
    /// Creates an Agent as test setup and authenticates through the API.
    /// </summary>
    private async Task<AuthResponse> CreateAgentAsync(HttpClient client)
    {
        const string password = "ValidPass!";
        string email = $"viewer-agent-{Guid.NewGuid():N}@example.com";

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository userRepository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();

        IPasswordHasher passwordHasher =
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var agent = new User
        {
            FirstName = "Ticket",
            LastName = "Agent",
            Email = email,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = UserRole.Agent
        };

        await userRepository.AddAsync(agent);

        return await LoginAsync(client, email, password);
    }

    /// <summary>
    /// Authenticates an existing User through the public API.
    /// </summary>
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

        AuthResponse? result =
            await response.Content.ReadFromJsonAsync<AuthResponse>();

        return result
            ?? throw new InvalidOperationException(
                "Auth response body was empty.");
    }

    /// <summary>
    /// Applies a JWT to subsequent requests made by the client.
    /// </summary>
    private static void SetBearerToken(
        HttpClient client,
        string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>
    /// Matches the API's snake_case enum JSON representation.
    /// </summary>
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(
            JsonSerializerDefaults.Web);

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.SnakeCaseLower,
                allowIntegerValues: false));

        return options;
    }
}
