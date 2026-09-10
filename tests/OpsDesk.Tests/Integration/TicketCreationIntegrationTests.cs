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
public sealed class TicketCreationIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private readonly OpsDeskApiFactory _factory;

    public TicketCreationIntegrationTests(
        OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    public static TheoryData<string?, string?> InvalidTicketContent =>
        new()
        {
            { "   ", "A valid description" },
            {
                new string(
                    'T',
                    Ticket.MaximumTitleLength + 1),
                "A valid description"
            },
            { "A valid title", "   " },
            {
                "A valid title",
                new string(
                    'D',
                    Ticket.MaximumDescriptionLength + 1)
            }
        };

    /// <summary>
    /// Verifies ticket creation through the public HTTP API.
    /// </summary>
    [Fact]
    public async Task Authenticated_customer_should_create_ticket()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client);

        _factory.VerifyAccount(customer.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                customer.AccessToken);

        var request = new
        {
            title = "Printer is unavailable",
            description =
                "The finance department printer does not respond."
        };

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/tickets",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        TicketResponse? ticket =
            await response.Content
                .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        Assert.NotNull(ticket);
        Assert.Equal(
            $"/tickets/{ticket.Id}",
            response.Headers.Location?.ToString());
        Assert.Equal("Printer is unavailable", ticket.Title);
        Assert.Equal(
            "The finance department printer does not respond.",
            ticket.Description);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Equal(customer.UserId, ticket.RequesterId);
        Assert.Null(ticket.AssigneeId);
        Assert.Equal(ticket.CreatedAtUtc, ticket.UpdatedAtUtc);
    }

    /// <summary>
    /// Verifies that an explicitly selected priority is preserved.
    /// </summary>
    [Fact]
    public async Task Explicit_priority_should_be_preserved()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client);

        _factory.VerifyAccount(customer.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                customer.AccessToken);

        var request = new
        {
            title = "Payroll export failed",
            description =
                "The monthly payroll export cannot be generated.",
            priority = "high"
        };

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/tickets",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        TicketResponse? ticket =
            await response.Content
                .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        Assert.NotNull(ticket);
        Assert.Equal(TicketPriority.High, ticket.Priority);
    }

    /// <summary>
    /// Verifies OpenAPI publishes Ticket text requirements for clients.
    /// </summary>
    [Fact]
    public async Task OpenApi_should_publish_ticket_content_limits()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document =
            await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());

        JsonElement requestSchema = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(nameof(CreateTicketRequest));

        JsonElement properties =
            requestSchema.GetProperty("properties");

        Assert.Equal(
            Ticket.MaximumTitleLength,
            properties
                .GetProperty("title")
                .GetProperty("maxLength")
                .GetInt32());

        Assert.Equal(
            Ticket.MaximumDescriptionLength,
            properties
                .GetProperty("description")
                .GetProperty("maxLength")
                .GetInt32());

        string[] requiredProperties = requestSchema
            .GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();

        Assert.Contains("title", requiredProperties);
        Assert.Contains("description", requiredProperties);
    }

    /// <summary>
    /// Verifies content exactly at both maximum lengths is accepted.
    /// </summary>
    [Fact]
    public async Task Maximum_length_ticket_content_should_be_accepted()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client);

        _factory.VerifyAccount(customer.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                customer.AccessToken);

        string title = new('T', Ticket.MaximumTitleLength);
        string description =
            new('D', Ticket.MaximumDescriptionLength);

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/tickets",
                new CreateTicketRequest(
                    title,
                    description));

        string responseBody =
            await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but received {response.StatusCode}. " +
            $"Response: {responseBody}");

        TicketResponse? ticket =
            await response.Content
                .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        Assert.NotNull(ticket);
        Assert.Equal(title, ticket.Title);
        Assert.Equal(description, ticket.Description);
    }

    /// <summary>
    /// Verifies invalid required content is rejected by the public API.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidTicketContent))]
    public async Task Invalid_ticket_content_should_return_bad_request(
        string? title,
        string? description)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client);

        _factory.VerifyAccount(customer.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                customer.AccessToken);

        var request = new
        {
            title,
            description
        };

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/tickets",
                request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    /// <summary>
    /// Verifies Ticket creation requires authentication.
    /// </summary>
    [Fact]
    public async Task Anonymous_user_should_not_create_ticket()
    {
        using HttpClient client = _factory.CreateClient();

        var request = new
        {
            title = "Printer is unavailable",
            description = "The printer does not respond."
        };

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/tickets",
                request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    /// <summary>
    /// Verifies unsupported priority names are rejected during model binding.
    /// </summary>
    [Fact]
    public async Task Unsupported_priority_should_return_bad_request()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client);

        _factory.VerifyAccount(customer.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                customer.AccessToken);

        var request = new
        {
            title = "Printer is unavailable",
            description = "The printer does not respond.",
            priority = "critical"
        };

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/tickets",
                request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    /// <summary>
    /// Verifies support roles can create Tickets for themselves.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public async Task Support_staff_should_create_ticket(
        UserRole role)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse staff = role switch
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

        _factory.VerifyAccount(staff.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                staff.AccessToken);

        var request = new
        {
            title = "Support tooling problem",
            description =
                "The support dashboard cannot be opened."
        };

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/tickets",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        TicketResponse? ticket =
            await response.Content
                .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        Assert.NotNull(ticket);
        Assert.Equal(staff.UserId, ticket.RequesterId);
    }

    /// <summary>
    /// Registers a unique Customer and returns the authentication response.
    /// </summary>
    private async Task<AuthResponse> RegisterAsync(
        HttpClient client)
    {
        string email =
            $"ticket-{Guid.NewGuid():N}@example.com";

        var request = new RegisterRequest(
            "Ticket",
            "Requester",
            email,
            "ValidPass!");

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/auth/register",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        AuthResponse? result =
            await response.Content
                .ReadFromJsonAsync<AuthResponse>();

        return result
            ?? throw new InvalidOperationException(
                "Auth response body was empty.");
    }

    /// <summary>
    /// Inserts an Agent for test setup and authenticates through the API.
    /// </summary>
    private async Task<AuthResponse> CreateAgentAsync(
        HttpClient client)
    {
        const string password = "ValidPass!";
        string email =
            $"agent-{Guid.NewGuid():N}@example.com";

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository repository =
            scope.ServiceProvider
                .GetRequiredService<IUserRepository>();

        IPasswordHasher passwordHasher =
            scope.ServiceProvider
                .GetRequiredService<IPasswordHasher>();

        var agent = new User
        {
            FirstName = "Support",
            LastName = "Agent",
            Email = email,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = UserRole.Agent
        };

        await repository.AddAsync(agent);

        return await LoginAsync(client, email, password);
    }

    /// <summary>
    /// Authenticates an existing User and returns the token response.
    /// </summary>
    private async Task<AuthResponse> LoginAsync(
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
            await response.Content
                .ReadFromJsonAsync<AuthResponse>();

        return result
            ?? throw new InvalidOperationException(
                "Auth response body was empty.");
    }

    /// <summary>
    /// Creates JSON options that match the API's enum wire format.
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
