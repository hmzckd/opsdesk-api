using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Sla.Interfaces;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class TicketActivityIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private readonly OpsDeskApiFactory _factory;

    public TicketActivityIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies assignment, status, and comment records share one chronological timeline.
    /// </summary>
    [Fact]
    public async Task Agent_should_view_mixed_activity_in_chronological_order()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent = await CreateAgentAsync(client);
        _factory.VerifyAccount(agent.AccessToken);        SetBearerToken(client, agent.AccessToken);

        HttpResponseMessage assignmentResponse =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = agent.UserId });

        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);

        HttpResponseMessage statusResponse =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                new { status = "in_progress" });

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        HttpResponseMessage commentResponse =
            await client.PostAsJsonAsync(
                $"/tickets/{ticket.Id}/comments",
                new { content = "Investigating the dashboard failure." });

        Assert.Equal(HttpStatusCode.Created, commentResponse.StatusCode);

        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{ticket.Id}/activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document =
            await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());

        JsonElement activity = document.RootElement;

        Assert.Equal(JsonValueKind.Array, activity.ValueKind);
        Assert.Equal(3, activity.GetArrayLength());

        Assert.Equal(
            "assignment_changed",
            activity[0].GetProperty("type").GetString());
        AssertActor(activity[0], agent.UserId, "Activity", "Agent", "agent");
        Assert.Equal(
            JsonValueKind.Null,
            activity[0].GetProperty("previousAssignee").ValueKind);
        Assert.Equal(
            agent.UserId,
            activity[0]
                .GetProperty("newAssignee")
                .GetProperty("id")
                .GetGuid());

        Assert.Equal(
            "status_changed",
            activity[1].GetProperty("type").GetString());
        AssertActor(activity[1], agent.UserId, "Activity", "Agent", "agent");
        Assert.Equal(
            "open",
            activity[1].GetProperty("previousStatus").GetString());
        Assert.Equal(
            "in_progress",
            activity[1].GetProperty("newStatus").GetString());

        Assert.Equal(
            "comment_added",
            activity[2].GetProperty("type").GetString());
        AssertActor(activity[2], agent.UserId, "Activity", "Agent", "agent");
        Assert.Equal(
            "Investigating the dashboard failure.",
            activity[2].GetProperty("commentContent").GetString());

        DateTime firstTime =
            activity[0].GetProperty("createdAtUtc").GetDateTime();
        DateTime secondTime =
            activity[1].GetProperty("createdAtUtc").GetDateTime();
        DateTime thirdTime =
            activity[2].GetProperty("createdAtUtc").GetDateTime();

        Assert.True(firstTime <= secondTime);
        Assert.True(secondTime <= thirdTime);
    }

    /// <summary>
    /// Verifies equal timestamps use activity type and identity tie-breakers.
    /// </summary>
    [Fact]
    public async Task Equal_time_activity_should_have_stable_order()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent = await CreateAgentAsync(client);
        _factory.VerifyAccount(agent.AccessToken);        SetBearerToken(client, agent.AccessToken);
        await AssignTicketAsync(client, ticket.Id, agent.UserId);
        await ChangeStatusAsync(client, ticket.Id, "in_progress");
        await AddCommentAsync(client, ticket.Id, "Second stable item.");
        await AddCommentAsync(client, ticket.Id, "First stable item.");

        DateTime sharedCreatedAtUtc = DateTime.UtcNow;
        Guid assignmentChangeId;
        Guid statusChangeId;
        Guid[] commentIds;

        await using (AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope())
        {
            OpsDeskDbContext dbContext =
                scope.ServiceProvider
                    .GetRequiredService<OpsDeskDbContext>();

            await dbContext.TicketComments
                .Where(item => item.TicketId == ticket.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    item => item.CreatedAtUtc,
                    sharedCreatedAtUtc));

            await dbContext.TicketStatusChanges
                .Where(item => item.TicketId == ticket.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    item => item.CreatedAtUtc,
                    sharedCreatedAtUtc));

            await dbContext.TicketAssignmentChanges
                .Where(item => item.TicketId == ticket.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    item => item.CreatedAtUtc,
                    sharedCreatedAtUtc));

            assignmentChangeId = await dbContext
                .TicketAssignmentChanges
                .Where(item => item.TicketId == ticket.Id)
                .Select(item => item.Id)
                .SingleAsync();

            statusChangeId = await dbContext.TicketStatusChanges
                .Where(item => item.TicketId == ticket.Id)
                .Select(item => item.Id)
                .SingleAsync();

            commentIds = await dbContext.TicketComments
                .Where(item => item.TicketId == ticket.Id)
                .Select(item => item.Id)
                .OrderBy(id => id)
                .ToArrayAsync();
        }

        TicketActivityResponse[] activity =
            await GetActivityAsync(client, ticket.Id);

        Guid[] expectedIds = commentIds
            .Concat(new[] { statusChangeId, assignmentChangeId })
            .ToArray();

        Assert.Equal(expectedIds, activity.Select(item => item.Id));
        Assert.Equal(
            new[]
            {
                TicketActivityType.CommentAdded,
                TicketActivityType.CommentAdded,
                TicketActivityType.StatusChanged,
                TicketActivityType.AssignmentChanged
            },
            activity.Select(item => item.Type));
    }

    /// <summary>
    /// Verifies a Customer sees public activity but not assignment history.
    /// </summary>
    [Fact]
    public async Task Customer_should_view_own_public_activity_only()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent = await CreateAgentAsync(client);
        _factory.VerifyAccount(agent.AccessToken);        SetBearerToken(client, agent.AccessToken);
        await AssignTicketAsync(client, ticket.Id, agent.UserId);
        await ChangeStatusAsync(client, ticket.Id, "in_progress");
        await AddCommentAsync(client, ticket.Id, "Public progress update.");

        _factory.VerifyAccount(requester.AccessToken);
        SetBearerToken(client, requester.AccessToken);
        TicketActivityResponse[] activity =
            await GetActivityAsync(client, ticket.Id);

        Assert.Equal(2, activity.Length);
        Assert.Contains(
            activity,
            item => item.Type == TicketActivityType.StatusChanged);
        Assert.Contains(
            activity,
            item => item.Type == TicketActivityType.CommentAdded);
        Assert.DoesNotContain(
            activity,
            item => item.Type == TicketActivityType.AssignmentChanged);
    }

    /// <summary>
    /// Verifies a Customer cannot discover another Customer's timeline.
    /// </summary>
    [Fact]
    public async Task Customer_viewing_another_activity_should_return_not_found()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse owner = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(owner.AccessToken);        SetBearerToken(client, owner.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse otherCustomer = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(otherCustomer.AccessToken);        SetBearerToken(client, otherCustomer.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{ticket.Id}/activity");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies an Agent cannot discover another Agent's assigned timeline.
    /// </summary>
    [Fact]
    public async Task Agent_viewing_another_agents_activity_should_return_not_found()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse ownerAgent = await CreateAgentAsync(client);
        _factory.VerifyAccount(ownerAgent.AccessToken);        SetBearerToken(client, ownerAgent.AccessToken);
        await AssignTicketAsync(client, ticket.Id, ownerAgent.UserId);

        AuthResponse otherAgent = await CreateAgentAsync(client);
        _factory.VerifyAccount(otherAgent.AccessToken);        SetBearerToken(client, otherAgent.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{ticket.Id}/activity");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies an Admin sees public and internal activity for any Ticket.
    /// </summary>
    [Fact]
    public async Task Admin_should_view_all_activity_types()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent = await CreateAgentAsync(client);
        _factory.VerifyAccount(agent.AccessToken);        SetBearerToken(client, agent.AccessToken);
        await AssignTicketAsync(client, ticket.Id, agent.UserId);
        await ChangeStatusAsync(client, ticket.Id, "in_progress");
        await AddCommentAsync(client, ticket.Id, "Admin-visible update.");

        AuthResponse admin =
            await CreateStaffUserAsync(client, UserRole.Admin);
        _factory.VerifyAccount(admin.AccessToken);        SetBearerToken(client, admin.AccessToken);

        TicketActivityResponse[] activity =
            await GetActivityAsync(client, ticket.Id);

        Assert.Equal(3, activity.Length);
        Assert.Contains(
            activity,
            item => item.Type == TicketActivityType.AssignmentChanged);
        Assert.Contains(
            activity,
            item => item.Type == TicketActivityType.StatusChanged);
        Assert.Contains(
            activity,
            item => item.Type == TicketActivityType.CommentAdded);
    }

    /// <summary>
    /// Verifies every authorized viewer sees a system-generated SLA breach.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public async Task Authorized_viewer_should_see_sla_breach_with_no_actor(
        UserRole viewerRole)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);
        AuthResponse viewer = requester;

        if (viewerRole == UserRole.Agent)
        {
            viewer = await CreateAgentAsync(client);
            _factory.VerifyAccount(viewer.AccessToken);
            SetBearerToken(client, viewer.AccessToken);
            await AssignTicketAsync(client, ticket.Id, viewer.UserId);
        }
        else if (viewerRole == UserRole.Admin)
        {
            viewer = await CreateStaffUserAsync(client, UserRole.Admin);
            _factory.VerifyAccount(viewer.AccessToken);
        }

        await using (AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope())
        {
            OpsDeskDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<OpsDeskDbContext>();

            await dbContext.Tickets
                .Where(item => item.Id == ticket.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    item => item.SlaDeadlineUtc,
                    DateTime.UtcNow.AddMinutes(-1)));

            ISlaBreachService breachService = scope.ServiceProvider
                .GetRequiredService<ISlaBreachService>();

            await breachService.DetectAndRecordAsync(100);
        }

        SetBearerToken(client, viewer.AccessToken);
        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{ticket.Id}/activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        JsonElement activity = document.RootElement;
        JsonElement slaActivity = activity.EnumerateArray().Single(
            item => item.GetProperty("type").GetString() ==
                "sla_breached");

        Assert.Equal(
            JsonValueKind.Null,
            slaActivity.GetProperty("actor").ValueKind);
    }

    /// <summary>
    /// Verifies a visible Ticket with no recorded activity returns an empty array.
    /// </summary>
    [Fact]
    public async Task Empty_activity_timeline_should_return_empty_array()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        TicketActivityResponse[] activity =
            await GetActivityAsync(client, ticket.Id);

        Assert.Empty(activity);
    }

    /// <summary>
    /// Verifies an authenticated User receives Not Found for an unknown Ticket.
    /// </summary>
    [Fact]
    public async Task Unknown_ticket_activity_should_return_not_found()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{Guid.NewGuid()}/activity");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies the activity endpoint requires a valid JWT.
    /// </summary>
    [Fact]
    public async Task Anonymous_activity_request_should_return_unauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{Guid.NewGuid()}/activity");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Registers a unique Customer through the public API.
    /// </summary>
    private async Task<AuthResponse> RegisterCustomerAsync(
        HttpClient client)
    {
        var request = new RegisterRequest(
            "Activity",
            "Requester",
            $"activity-{Guid.NewGuid():N}@example.com",
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
    private async Task<TicketResponse> CreateTicketAsync(
        HttpClient client)
    {
        var request = new CreateTicketRequest(
            "Dashboard access fails",
            "The operations dashboard returns an access denied message.");

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
    /// Persists a test Agent and logs in through the public API.
    /// </summary>
    private Task<AuthResponse> CreateAgentAsync(HttpClient client)
    {
        return CreateStaffUserAsync(client, UserRole.Agent);
    }

    /// <summary>
    /// Persists a support User and logs in through the public API.
    /// </summary>
    private async Task<AuthResponse> CreateStaffUserAsync(
        HttpClient client,
        UserRole role)
    {
        const string password = "ValidPass!";
        string normalizedRole = role.ToString().ToLowerInvariant();
        string email =
            $"activity-{normalizedRole}-{Guid.NewGuid():N}@example.com";

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository userRepository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IPasswordHasher passwordHasher =
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            FirstName = "Activity",
            LastName = role.ToString(),
            Email = email,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = role
        };

        await userRepository.AddAsync(user);

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
    /// Claims one Ticket for the Agent used by the test.
    /// </summary>
    private async Task AssignTicketAsync(
        HttpClient client,
        Guid ticketId,
        Guid agentId)
    {
        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/tickets/{ticketId}/assignee",
            new { assigneeId = agentId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Performs one required status transition through the public API.
    /// </summary>
    private async Task ChangeStatusAsync(
        HttpClient client,
        Guid ticketId,
        string status)
    {
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/tickets/{ticketId}/status",
            new { status });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Adds one required public Comment through the public API.
    /// </summary>
    private async Task AddCommentAsync(
        HttpClient client,
        Guid ticketId,
        string content)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticketId}/comments",
            new { content });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Reads and deserializes a successful activity response.
    /// </summary>
    private async Task<TicketActivityResponse[]> GetActivityAsync(
        HttpClient client,
        Guid ticketId)
    {
        HttpResponseMessage response =
            await client.GetAsync($"/tickets/{ticketId}/activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketActivityResponse[]? activity =
            await response.Content.ReadFromJsonAsync<
                TicketActivityResponse[]>(JsonOptions);

        return activity
            ?? throw new InvalidOperationException(
                "Activity response body was empty.");
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
    /// Verifies the public actor summary embedded in an activity item.
    /// </summary>
    private static void AssertActor(
        JsonElement activityItem,
        Guid expectedId,
        string expectedFirstName,
        string expectedLastName,
        string expectedRole)
    {
        JsonElement actor = activityItem.GetProperty("actor");

        Assert.Equal(expectedId, actor.GetProperty("id").GetGuid());
        Assert.Equal(
            expectedFirstName,
            actor.GetProperty("firstName").GetString());
        Assert.Equal(
            expectedLastName,
            actor.GetProperty("lastName").GetString());
        Assert.Equal(
            expectedRole,
            actor.GetProperty("role").GetString());
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
