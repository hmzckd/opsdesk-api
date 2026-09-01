using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpsDesk.Application.Agents.DTOs;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Tickets.DTOs;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class TicketListingIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private readonly OpsDeskApiFactory _factory;

    public TicketListingIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies a Customer receives only Tickets they requested.
    /// </summary>
    [Fact]
    public async Task Customer_should_list_only_own_tickets()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "Owner");
        SetBearerToken(client, customer.AccessToken);

        TicketResponse firstTicket = await CreateTicketAsync(
            client,
            "First owned ticket");

        TicketResponse secondTicket = await CreateTicketAsync(
            client,
            "Second owned ticket");

        AuthResponse otherCustomer = await RegisterAsync(client, "Other");
        SetBearerToken(client, otherCustomer.AccessToken);
        await CreateTicketAsync(client, "Another customer's ticket");

        SetBearerToken(client, customer.AccessToken);

        HttpResponseMessage response = await client.GetAsync("/tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope? result =
            await response.Content.ReadFromJsonAsync<TicketListEnvelope>();

        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);

        Guid[] returnedIds = result.Items
            .Select(ticket => ticket.Id)
            .ToArray();

        Assert.Equal(
            [secondTicket.Id, firstTicket.Id],
            returnedIds);
    }

    /// <summary>
    /// Verifies an Agent sees unassigned and self-assigned Tickets only.
    /// </summary>
    [Fact]
    public async Task Agent_should_list_unassigned_and_own_tickets()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "AgentScope");
        SetBearerToken(client, customer.AccessToken);

        TicketResponse unassignedTicket = await CreateTicketAsync(
            client,
            "Visible unassigned ticket");

        TicketResponse ownTicket = await CreateTicketAsync(
            client,
            "Visible assigned ticket");

        TicketResponse otherAgentsTicket = await CreateTicketAsync(
            client,
            "Hidden assigned ticket");

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);

        SetBearerToken(client, admin.AccessToken);

        AgentCredentials viewerAgent = await CreateAgentAsync(client);
        AgentCredentials otherAgent = await CreateAgentAsync(client);

        await AssignTicketAsync(client, ownTicket.Id, viewerAgent.Id);
        await AssignTicketAsync(
            client,
            otherAgentsTicket.Id,
            otherAgent.Id);

        AuthResponse agent = await LoginAsync(
            client,
            viewerAgent.Email,
            viewerAgent.Password);

        SetBearerToken(client, agent.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync("/tickets?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope? result =
            await response.Content.ReadFromJsonAsync<TicketListEnvelope>();

        Assert.NotNull(result);

        Guid[] returnedIds = result.Items
            .Select(ticket => ticket.Id)
            .ToArray();

        Assert.Contains(unassignedTicket.Id, returnedIds);
        Assert.Contains(ownTicket.Id, returnedIds);
        Assert.DoesNotContain(otherAgentsTicket.Id, returnedIds);
    }

    /// <summary>
    /// Verifies an Admin can list Tickets across requester boundaries.
    /// </summary>
    [Fact]
    public async Task Admin_should_list_all_tickets()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse firstCustomer = await RegisterAsync(client, "FirstAdmin");
        SetBearerToken(client, firstCustomer.AccessToken);
        TicketResponse firstTicket = await CreateTicketAsync(
            client,
            "First Admin-visible ticket");

        AuthResponse secondCustomer = await RegisterAsync(
            client,
            "SecondAdmin");
        SetBearerToken(client, secondCustomer.AccessToken);
        TicketResponse secondTicket = await CreateTicketAsync(
            client,
            "Second Admin-visible ticket");

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);

        SetBearerToken(client, admin.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync("/tickets?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope? result =
            await response.Content.ReadFromJsonAsync<TicketListEnvelope>();

        Assert.NotNull(result);

        Guid[] returnedIds = result.Items
            .Select(ticket => ticket.Id)
            .ToArray();

        Assert.Contains(firstTicket.Id, returnedIds);
        Assert.Contains(secondTicket.Id, returnedIds);
    }

    /// <summary>
    /// Verifies page offsets, metadata, and newest-first ordering.
    /// </summary>
    [Fact]
    public async Task Customer_should_receive_requested_ticket_page()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "Pagination");
        SetBearerToken(client, customer.AccessToken);

        await CreateTicketAsync(client, "Oldest paged ticket");
        TicketResponse middleTicket = await CreateTicketAsync(
            client,
            "Middle paged ticket");
        await CreateTicketAsync(client, "Newest paged ticket");

        HttpResponseMessage response =
            await client.GetAsync("/tickets?page=2&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope? result =
            await response.Content.ReadFromJsonAsync<TicketListEnvelope>();

        Assert.NotNull(result);
        Assert.Equal([middleTicket.Id], result.Items.Select(item => item.Id));
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    /// <summary>
    /// Verifies page numbers start at one.
    /// </summary>
    [Fact]
    public async Task Page_zero_should_return_problem_details()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "InvalidPage");
        SetBearerToken(client, customer.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync("/tickets?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Verifies page size remains inside the approved one-to-100 range.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Invalid_page_size_should_return_problem_details(
        int pageSize)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "InvalidSize");
        SetBearerToken(client, customer.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync($"/tickets?pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Verifies a valid query with no visible Tickets returns an empty page.
    /// </summary>
    [Fact]
    public async Task Customer_without_tickets_should_receive_empty_page()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "EmptyPage");
        SetBearerToken(client, customer.AccessToken);

        HttpResponseMessage response = await client.GetAsync("/tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope? result =
            await response.Content.ReadFromJsonAsync<TicketListEnvelope>();

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    /// <summary>
    /// Verifies a page beyond the final page preserves collection totals.
    /// </summary>
    [Fact]
    public async Task Page_after_final_page_should_return_empty_items()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "BeyondPage");
        SetBearerToken(client, customer.AccessToken);
        await CreateTicketAsync(client, "Only paged ticket");

        HttpResponseMessage response =
            await client.GetAsync("/tickets?page=2&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope? result =
            await response.Content.ReadFromJsonAsync<TicketListEnvelope>();

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    /// <summary>
    /// Verifies collection items omit fields reserved for Ticket details.
    /// </summary>
    [Fact]
    public async Task Ticket_list_should_return_compact_items()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "CompactItem");
        SetBearerToken(client, customer.AccessToken);
        await CreateTicketAsync(client, "Compact listed ticket");

        HttpResponseMessage response = await client.GetAsync("/tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        JsonElement item = body.GetProperty("items")[0];

        Assert.True(item.TryGetProperty("id", out _));
        Assert.True(item.TryGetProperty("title", out _));
        Assert.True(item.TryGetProperty("priority", out _));
        Assert.True(item.TryGetProperty("status", out _));
        Assert.True(item.TryGetProperty("requesterId", out _));
        Assert.True(item.TryGetProperty("assigneeId", out _));
        Assert.True(item.TryGetProperty("createdAtUtc", out _));
        Assert.True(item.TryGetProperty("updatedAtUtc", out _));
        Assert.False(item.TryGetProperty("description", out _));
        Assert.False(item.TryGetProperty("resolvedAtUtc", out _));
        Assert.False(item.TryGetProperty("closedAtUtc", out _));
    }

    /// <summary>
    /// Verifies Ticket listing requires authentication.
    /// </summary>
    [Fact]
    public async Task Anonymous_user_should_not_list_tickets()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Registers a unique Customer through the public authentication API.
    /// </summary>
    private static async Task<AuthResponse> RegisterAsync(
        HttpClient client,
        string lastName)
    {
        var request = new RegisterRequest(
            "Listing",
            lastName,
            $"listing-{Guid.NewGuid():N}@example.com",
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
    /// Provisions an Agent through the Admin API for test setup.
    /// </summary>
    private static async Task<AgentCredentials> CreateAgentAsync(
        HttpClient client)
    {
        const string password = "AgentPass!";
        string email = $"listing-agent-{Guid.NewGuid():N}@example.com";

        var request = new CreateAgentRequest(
            "Listing",
            "Agent",
            email,
            password);

        HttpResponseMessage response =
            await client.PostAsJsonAsync("/admin/agents", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        AgentResponse? result =
            await response.Content.ReadFromJsonAsync<AgentResponse>();

        Assert.NotNull(result);

        return new AgentCredentials(result.Id, email, password);
    }

    /// <summary>
    /// Assigns one Ticket through the public Admin assignment endpoint.
    /// </summary>
    private static async Task AssignTicketAsync(
        HttpClient client,
        Guid ticketId,
        Guid assigneeId)
    {
        HttpResponseMessage response =
            await client.PutAsJsonAsync(
                $"/tickets/{ticketId}/assignee",
                new AssignTicketRequest(assigneeId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Creates a Ticket through the public Ticket API.
    /// </summary>
    private static async Task<TicketResponse> CreateTicketAsync(
        HttpClient client,
        string title)
    {
        var request = new CreateTicketRequest(
            title,
            "Ticket listing integration test description.");

        HttpResponseMessage response =
            await client.PostAsJsonAsync("/tickets", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        TicketResponse? result =
            await response.Content.ReadFromJsonAsync<TicketResponse>(
                JsonOptions);

        return result
            ?? throw new InvalidOperationException(
                "Ticket response body was empty.");
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

    private sealed record TicketListEnvelope(
        IReadOnlyList<TicketListItem> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages,
        bool HasPreviousPage,
        bool HasNextPage);

    private sealed record TicketListItem(Guid Id);

    private sealed record AgentCredentials(
        Guid Id,
        string Email,
        string Password);
}
