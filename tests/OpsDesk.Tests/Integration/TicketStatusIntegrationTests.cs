using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class TicketStatusIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private readonly OpsDeskApiFactory _factory;

    public TicketStatusIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies an Agent can start work on an open Ticket.
    /// </summary>
    [Fact]
    public async Task Agent_should_start_work_on_open_ticket()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);

        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent = await CreateAgentAsync(client);
        SetBearerToken(client, agent.AccessToken);

        var request = new
        {
            status = "in_progress"
        };

        HttpResponseMessage response =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketResponse? updatedTicket =
            await response.Content
                .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        Assert.NotNull(updatedTicket);
        Assert.Equal(ticket.Id, updatedTicket.Id);
        Assert.Equal(TicketStatus.InProgress, updatedTicket.Status);
        Assert.True(updatedTicket.UpdatedAtUtc >= ticket.UpdatedAtUtc);
    }

    /// <summary>
    /// Verifies the Ticket update and status-change record persist together.
    /// </summary>
    [Fact]
    public async Task Successful_transition_should_persist_status_change()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);

        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent = await CreateAgentAsync(client);
        SetBearerToken(client, agent.AccessToken);

        HttpResponseMessage response =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                new { status = "in_progress" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);

        TicketStatusChange statusChange =
            await dbContext.TicketStatusChanges
                .AsNoTracking()
                .SingleAsync(item => item.TicketId == ticket.Id);

        Assert.Equal(TicketStatus.InProgress, persistedTicket.Status);
        Assert.Equal(agent.UserId, statusChange.ActorId);
        Assert.Equal(TicketStatus.Open, statusChange.PreviousStatus);
        Assert.Equal(TicketStatus.InProgress, statusChange.NewStatus);
        Assert.Equal(ticket.Id, statusChange.TicketId);
        Assert.Equal(DateTimeKind.Utc, statusChange.CreatedAtUtc.Kind);
    }

    /// <summary>
    /// Verifies a requester sees status history in chronological order.
    /// </summary>
    [Fact]
    public async Task Requester_should_view_chronological_status_history()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent = await CreateAgentAsync(client);
        SetBearerToken(client, agent.AccessToken);

        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "in_progress")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "waiting_customer")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "in_progress")).StatusCode);

        SetBearerToken(client, requester.AccessToken);
        HttpResponseMessage response = await client.GetAsync(
            $"/tickets/{ticket.Id}/status-history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document =
            await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());

        JsonElement history = document.RootElement;

        Assert.Equal(JsonValueKind.Array, history.ValueKind);
        Assert.Equal(3, history.GetArrayLength());

        AssertStatusHistoryItem(
            history[0],
            agent.UserId,
            "open",
            "in_progress");
        AssertStatusHistoryItem(
            history[1],
            agent.UserId,
            "in_progress",
            "waiting_customer");
        AssertStatusHistoryItem(
            history[2],
            agent.UserId,
            "waiting_customer",
            "in_progress");

        DateTime firstChange = history[0]
            .GetProperty("createdAtUtc")
            .GetDateTime();
        DateTime secondChange = history[1]
            .GetProperty("createdAtUtc")
            .GetDateTime();
        DateTime thirdChange = history[2]
            .GetProperty("createdAtUtc")
            .GetDateTime();

        Assert.True(firstChange <= secondChange);
        Assert.True(secondChange <= thirdChange);
    }

    /// <summary>
    /// Verifies a Customer cannot discover another Ticket's history.
    /// </summary>
    [Fact]
    public async Task Customer_viewing_another_history_should_return_not_found()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse owner = await RegisterCustomerAsync(client);
        SetBearerToken(client, owner.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse otherCustomer = await RegisterCustomerAsync(client);
        SetBearerToken(client, otherCustomer.AccessToken);

        HttpResponseMessage response = await client.GetAsync(
            $"/tickets/{ticket.Id}/status-history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies an invalid transition returns Conflict without persistence.
    /// </summary>
    [Fact]
    public async Task Invalid_transition_should_return_conflict_without_changes()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent = await CreateAgentAsync(client);
        SetBearerToken(client, agent.AccessToken);

        HttpResponseMessage response =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                new { status = "resolved" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);
        int statusChangeCount = await dbContext.TicketStatusChanges
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticket.Id);

        Assert.Equal(TicketStatus.Open, persistedTicket.Status);
        Assert.Equal(0, statusChangeCount);
    }

    /// <summary>
    /// Verifies a Customer cannot perform an Agent-owned transition.
    /// </summary>
    [Fact]
    public async Task Customer_operational_transition_should_return_forbidden()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        HttpResponseMessage response =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                new { status = "in_progress" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);
        int eventCount = await dbContext.TicketStatusChanges
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticket.Id);

        Assert.Equal(TicketStatus.Open, persistedTicket.Status);
        Assert.Equal(0, eventCount);
    }

    /// <summary>
    /// Verifies a Customer can reopen and close their own resolved Ticket.
    /// </summary>
    [Fact]
    public async Task Customer_should_manage_own_resolved_ticket()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent = await CreateAgentAsync(client);
        SetBearerToken(client, agent.AccessToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "in_progress")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "resolved")).StatusCode);

        SetBearerToken(client, requester.AccessToken);
        HttpResponseMessage reopenResponse =
            await ChangeStatusAsync(
                client,
                ticket.Id,
                "in_progress");

        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);
        TicketResponse reopenedTicket =
            await ReadTicketAsync(reopenResponse);
        Assert.Equal(TicketStatus.InProgress, reopenedTicket.Status);
        Assert.Null(reopenedTicket.ResolvedAtUtc);

        SetBearerToken(client, agent.AccessToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "resolved")).StatusCode);

        SetBearerToken(client, requester.AccessToken);
        HttpResponseMessage closeResponse =
            await ChangeStatusAsync(client, ticket.Id, "closed");

        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        TicketResponse closedTicket = await ReadTicketAsync(closeResponse);
        Assert.Equal(TicketStatus.Closed, closedTicket.Status);
        Assert.NotNull(closedTicket.ResolvedAtUtc);
        Assert.NotNull(closedTicket.ClosedAtUtc);
    }

    /// <summary>
    /// Verifies support roles can resolve a Ticket but cannot confirm closure.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public async Task Support_user_closing_resolved_ticket_should_be_forbidden(
        UserRole supportRole)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse supportUser =
            await CreateStaffUserAsync(client, supportRole);
        SetBearerToken(client, supportUser.AccessToken);

        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "in_progress")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "resolved")).StatusCode);

        HttpResponseMessage response =
            await ChangeStatusAsync(client, ticket.Id, "closed");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);
        int eventCount = await dbContext.TicketStatusChanges
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticket.Id);

        Assert.Equal(TicketStatus.Resolved, persistedTicket.Status);
        Assert.NotNull(persistedTicket.ResolvedAtUtc);
        Assert.Null(persistedTicket.ClosedAtUtc);
        Assert.Equal(2, eventCount);
    }

    /// <summary>
    /// Verifies Customers cannot discover another requester's Ticket.
    /// </summary>
    [Fact]
    public async Task Customer_changing_another_ticket_should_return_not_found()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse owner = await RegisterCustomerAsync(client);
        SetBearerToken(client, owner.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse otherCustomer = await RegisterCustomerAsync(client);
        SetBearerToken(client, otherCustomer.AccessToken);

        HttpResponseMessage response =
            await ChangeStatusAsync(
                client,
                ticket.Id,
                "in_progress");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies status changes require an authenticated User.
    /// </summary>
    [Fact]
    public async Task Anonymous_status_change_should_return_unauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response =
            await ChangeStatusAsync(
                client,
                Guid.NewGuid(),
                "in_progress");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies unsupported status text is rejected during JSON binding.
    /// </summary>
    [Fact]
    public async Task Malformed_status_should_return_bad_request()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse agent = await CreateAgentAsync(client);
        SetBearerToken(client, agent.AccessToken);

        HttpResponseMessage response =
            await ChangeStatusAsync(
                client,
                Guid.NewGuid(),
                "not_a_status");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Verifies a visible role receives Not Found for an unknown Ticket.
    /// </summary>
    [Fact]
    public async Task Unknown_ticket_should_return_not_found()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse agent = await CreateAgentAsync(client);
        SetBearerToken(client, agent.AccessToken);

        HttpResponseMessage response =
            await ChangeStatusAsync(
                client,
                Guid.NewGuid(),
                "in_progress");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies repeating the current status is a lifecycle conflict.
    /// </summary>
    [Fact]
    public async Task No_op_transition_should_return_conflict()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent = await CreateAgentAsync(client);
        SetBearerToken(client, agent.AccessToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "in_progress")).StatusCode);

        HttpResponseMessage response =
            await ChangeStatusAsync(
                client,
                ticket.Id,
                "in_progress");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Verifies an Agent can follow the operational lifecycle to resolution.
    /// </summary>
    [Fact]
    public async Task Agent_should_follow_operational_lifecycle_to_resolved()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent = await CreateAgentAsync(client);
        SetBearerToken(client, agent.AccessToken);

        string[] statuses =
        [
            "in_progress",
            "waiting_customer",
            "in_progress",
            "resolved"
        ];

        foreach (string status in statuses)
        {
            HttpResponseMessage response =
                await ChangeStatusAsync(client, ticket.Id, status);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);
        int eventCount = await dbContext.TicketStatusChanges
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticket.Id);

        Assert.Equal(TicketStatus.Resolved, persistedTicket.Status);
        Assert.NotNull(persistedTicket.ResolvedAtUtc);
        Assert.Null(persistedTicket.ClosedAtUtc);
        Assert.Equal(statuses.Length, eventCount);
    }

    /// <summary>
    /// Verifies an Admin has support-level lifecycle permission.
    /// </summary>
    [Fact]
    public async Task Admin_should_change_ticket_status()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse admin =
            await CreateStaffUserAsync(client, UserRole.Admin);
        SetBearerToken(client, admin.AccessToken);

        HttpResponseMessage response =
            await ChangeStatusAsync(
                client,
                ticket.Id,
                "in_progress");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Registers a unique Customer through the public API.
    /// </summary>
    private static async Task<AuthResponse> RegisterCustomerAsync(
        HttpClient client)
    {
        var request = new RegisterRequest(
            "Status",
            "Requester",
            $"status-{Guid.NewGuid():N}@example.com",
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
    /// Creates an open Ticket through the public API.
    /// </summary>
    private static async Task<TicketResponse> CreateTicketAsync(
        HttpClient client)
    {
        var request = new CreateTicketRequest(
            "Cannot access the operations dashboard",
            "The dashboard returns an access denied message.");

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
    /// Sends one status-change request through the public endpoint.
    /// </summary>
    private static Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        Guid ticketId,
        string status)
    {
        return client.PatchAsJsonAsync(
            $"/tickets/{ticketId}/status",
            new { status });
    }

    /// <summary>
    /// Reads a required Ticket response with the API's enum format.
    /// </summary>
    private static async Task<TicketResponse> ReadTicketAsync(
        HttpResponseMessage response)
    {
        TicketResponse? ticket =
            await response.Content
                .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        return ticket
            ?? throw new InvalidOperationException(
                "Ticket response body was empty.");
    }

    /// <summary>
    /// Creates an Agent as test setup and authenticates through the API.
    /// </summary>
    private async Task<AuthResponse> CreateAgentAsync(HttpClient client)
    {
        return await CreateStaffUserAsync(client, UserRole.Agent);
    }

    /// <summary>
    /// Creates a support User as test setup and authenticates through the API.
    /// </summary>
    private async Task<AuthResponse> CreateStaffUserAsync(
        HttpClient client,
        UserRole role)
    {
        const string password = "ValidPass!";
        string normalizedRole = role.ToString().ToLowerInvariant();
        string email =
            $"status-{normalizedRole}-{Guid.NewGuid():N}@example.com";

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository userRepository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();

        IPasswordHasher passwordHasher =
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            FirstName = "Status",
            LastName = role.ToString(),
            Email = email,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = role
        };

        await userRepository.AddAsync(user);

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
    /// Verifies one status-history item's public JSON contract.
    /// </summary>
    private static void AssertStatusHistoryItem(
        JsonElement item,
        Guid expectedActorId,
        string expectedPreviousStatus,
        string expectedNewStatus)
    {
        Assert.NotEqual(
            Guid.Empty,
            item.GetProperty("id").GetGuid());
        Assert.Equal(
            expectedActorId,
            item.GetProperty("actorId").GetGuid());
        Assert.Equal(
            expectedPreviousStatus,
            item.GetProperty("previousStatus").GetString());
        Assert.Equal(
            expectedNewStatus,
            item.GetProperty("newStatus").GetString());
        Assert.Equal(
            DateTimeKind.Utc,
            item.GetProperty("createdAtUtc").GetDateTime().Kind);
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
