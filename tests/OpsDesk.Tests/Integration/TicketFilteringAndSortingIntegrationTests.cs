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
public sealed class TicketFilteringAndSortingIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private readonly OpsDeskApiFactory _factory;

    public TicketFilteringAndSortingIntegrationTests(
        OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies status filtering accepts the API's snake_case enum values.
    /// </summary>
    [Fact]
    public async Task Customer_should_filter_tickets_by_status()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "StatusFilter");
        SetBearerToken(client, customer.AccessToken);

        await CreateTicketAsync(client, "Ticket remaining open");
        TicketResponse inProgressTicket = await CreateTicketAsync(
            client,
            "Ticket moving to in progress");

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);

        SetBearerToken(client, admin.AccessToken);
        AgentCredentials agent = await CreateAgentAsync(client);
        await AssignTicketAsync(client, inProgressTicket.Id, agent.Id);

        AuthResponse authenticatedAgent = await LoginAsync(
            client,
            agent.Email,
            agent.Password);

        SetBearerToken(client, authenticatedAgent.AccessToken);
        await ChangeStatusAsync(
            client,
            inProgressTicket.Id,
            "in_progress");

        SetBearerToken(client, customer.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync("/tickets?status=in_progress");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope result = await ReadListAsync(response);

        TicketListItem item = Assert.Single(result.Items);
        Assert.Equal(inProgressTicket.Id, item.Id);
        Assert.Equal("in_progress", item.Status);
        Assert.Equal(1, result.TotalCount);
    }

    /// <summary>
    /// Verifies priority filtering returns only the requested urgency.
    /// </summary>
    [Fact]
    public async Task Customer_should_filter_tickets_by_priority()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "PriorityFilter");
        SetBearerToken(client, customer.AccessToken);

        await CreateTicketAsync(client, "Low-priority ticket", "low");
        TicketResponse highPriorityTicket = await CreateTicketAsync(
            client,
            "High-priority ticket",
            "high");

        HttpResponseMessage response =
            await client.GetAsync("/tickets?priority=high");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope result = await ReadListAsync(response);

        TicketListItem item = Assert.Single(result.Items);
        Assert.Equal(highPriorityTicket.Id, item.Id);
        Assert.Equal("high", item.Priority);
        Assert.Equal(1, result.TotalCount);
    }

    /// <summary>
    /// Verifies an Admin can filter visible Tickets by requester identity.
    /// </summary>
    [Fact]
    public async Task Admin_should_filter_tickets_by_requester()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse firstCustomer = await RegisterAsync(
            client,
            "FirstRequester");
        SetBearerToken(client, firstCustomer.AccessToken);
        TicketResponse firstTicket = await CreateTicketAsync(
            client,
            "First requester's ticket");

        AuthResponse secondCustomer = await RegisterAsync(
            client,
            "SecondRequester");
        SetBearerToken(client, secondCustomer.AccessToken);
        TicketResponse secondTicket = await CreateTicketAsync(
            client,
            "Second requester's ticket");

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);
        SetBearerToken(client, admin.AccessToken);

        HttpResponseMessage response = await client.GetAsync(
            $"/tickets?requesterId={firstCustomer.UserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope result = await ReadListAsync(response);

        TicketListItem item = Assert.Single(result.Items);
        Assert.Equal(firstTicket.Id, item.Id);
        Assert.Equal(firstCustomer.UserId, item.RequesterId);
        Assert.DoesNotContain(
            result.Items,
            ticket => ticket.Id == secondTicket.Id);
    }

    /// <summary>
    /// Verifies an Admin can filter visible Tickets by assignee identity.
    /// </summary>
    [Fact]
    public async Task Admin_should_filter_tickets_by_assignee()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "AssigneeFilter");
        SetBearerToken(client, customer.AccessToken);
        TicketResponse firstTicket = await CreateTicketAsync(
            client,
            "First assigned ticket");
        TicketResponse secondTicket = await CreateTicketAsync(
            client,
            "Second assigned ticket");

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);
        SetBearerToken(client, admin.AccessToken);

        AgentCredentials firstAgent = await CreateAgentAsync(client);
        AgentCredentials secondAgent = await CreateAgentAsync(client);
        await AssignTicketAsync(client, firstTicket.Id, firstAgent.Id);
        await AssignTicketAsync(client, secondTicket.Id, secondAgent.Id);

        HttpResponseMessage response = await client.GetAsync(
            $"/tickets?assigneeId={firstAgent.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope result = await ReadListAsync(response);

        TicketListItem item = Assert.Single(result.Items);
        Assert.Equal(firstTicket.Id, item.Id);
        Assert.Equal(firstAgent.Id, item.AssigneeId);
    }

    /// <summary>
    /// Verifies unassigned filtering composes with another approved filter.
    /// </summary>
    [Fact]
    public async Task Admin_should_filter_unassigned_tickets()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "UnassignedFilter");
        SetBearerToken(client, customer.AccessToken);
        TicketResponse unassignedTicket = await CreateTicketAsync(
            client,
            "Unassigned filtered ticket");
        TicketResponse assignedTicket = await CreateTicketAsync(
            client,
            "Assigned filtered ticket");

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);
        SetBearerToken(client, admin.AccessToken);

        AgentCredentials agent = await CreateAgentAsync(client);
        await AssignTicketAsync(client, assignedTicket.Id, agent.Id);

        HttpResponseMessage response = await client.GetAsync(
            $"/tickets?requesterId={customer.UserId}&unassigned=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope result = await ReadListAsync(response);

        TicketListItem item = Assert.Single(result.Items);
        Assert.Equal(unassignedTicket.Id, item.Id);
        Assert.Null(item.AssigneeId);
    }

    /// <summary>
    /// Verifies approved filters compose inside one visible query scope.
    /// </summary>
    [Fact]
    public async Task Admin_should_combine_ticket_filters()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "CombinedFilter");
        SetBearerToken(client, customer.AccessToken);
        await CreateTicketAsync(client, "Combined low ticket", "low");
        TicketResponse matchingTicket = await CreateTicketAsync(
            client,
            "Combined matching ticket",
            "high");

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);
        SetBearerToken(client, admin.AccessToken);

        string query =
            $"/tickets?requesterId={customer.UserId}" +
            "&status=open&priority=high&unassigned=true";

        HttpResponseMessage response = await client.GetAsync(query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope result = await ReadListAsync(response);

        TicketListItem item = Assert.Single(result.Items);
        Assert.Equal(matchingTicket.Id, item.Id);
    }

    /// <summary>
    /// Verifies filters cannot expand a Customer's role visibility scope.
    /// </summary>
    [Fact]
    public async Task Requester_filter_should_not_expose_another_customers_ticket()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse firstCustomer = await RegisterAsync(
            client,
            "VisibleRequester");
        SetBearerToken(client, firstCustomer.AccessToken);
        await CreateTicketAsync(client, "Visible own ticket");

        AuthResponse secondCustomer = await RegisterAsync(
            client,
            "HiddenRequester");
        SetBearerToken(client, secondCustomer.AccessToken);
        await CreateTicketAsync(client, "Hidden other ticket");

        SetBearerToken(client, firstCustomer.AccessToken);

        HttpResponseMessage response = await client.GetAsync(
            $"/tickets?requesterId={secondCustomer.UserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope result = await ReadListAsync(response);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    /// <summary>
    /// Verifies creation-time ascending sorting returns oldest Tickets first.
    /// </summary>
    [Fact]
    public async Task Customer_should_sort_tickets_by_creation_time_ascending()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "CreatedSort");
        SetBearerToken(client, customer.AccessToken);

        TicketResponse oldestTicket = await CreateTicketAsync(
            client,
            "Oldest created ticket");
        TicketResponse middleTicket = await CreateTicketAsync(
            client,
            "Middle created ticket");
        TicketResponse newestTicket = await CreateTicketAsync(
            client,
            "Newest created ticket");

        HttpResponseMessage response = await client.GetAsync(
            "/tickets?sortBy=createdAtUtc&sortDirection=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope result = await ReadListAsync(response);

        Assert.Equal(
            [oldestTicket.Id, middleTicket.Id, newestTicket.Id],
            result.Items.Select(ticket => ticket.Id));
    }

    /// <summary>
    /// Verifies updated-time descending sorting returns the latest change first.
    /// </summary>
    [Fact]
    public async Task Customer_should_sort_tickets_by_updated_time_descending()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "UpdatedSort");
        SetBearerToken(client, customer.AccessToken);

        TicketResponse firstTicket = await CreateTicketAsync(
            client,
            "Ticket updated last");
        TicketResponse secondTicket = await CreateTicketAsync(
            client,
            "Ticket left unchanged");

        await AddCommentAsync(client, firstTicket.Id);

        HttpResponseMessage response = await client.GetAsync(
            "/tickets?sortBy=updatedAtUtc&sortDirection=desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope result = await ReadListAsync(response);

        Assert.Equal(
            [firstTicket.Id, secondTicket.Id],
            result.Items.Select(ticket => ticket.Id));
    }

    /// <summary>
    /// Verifies priority ascending sorting follows the domain urgency order.
    /// </summary>
    [Fact]
    public async Task Customer_should_sort_tickets_by_priority_ascending()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "PrioritySortAsc");
        SetBearerToken(client, customer.AccessToken);

        TicketResponse urgentTicket = await CreateTicketAsync(
            client,
            "Urgent sorting ticket",
            "urgent");
        TicketResponse lowTicket = await CreateTicketAsync(
            client,
            "Low sorting ticket",
            "low");
        TicketResponse highTicket = await CreateTicketAsync(
            client,
            "High sorting ticket",
            "high");
        TicketResponse mediumTicket = await CreateTicketAsync(
            client,
            "Medium sorting ticket",
            "medium");

        HttpResponseMessage response = await client.GetAsync(
            "/tickets?sortBy=priority&sortDirection=asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope result = await ReadListAsync(response);

        Assert.Equal(
            [lowTicket.Id, mediumTicket.Id, highTicket.Id, urgentTicket.Id],
            result.Items.Select(ticket => ticket.Id));
    }

    /// <summary>
    /// Verifies priority descending sorting returns urgent Tickets first.
    /// </summary>
    [Fact]
    public async Task Customer_should_sort_tickets_by_priority_descending()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "PrioritySortDesc");
        SetBearerToken(client, customer.AccessToken);

        TicketResponse mediumTicket = await CreateTicketAsync(
            client,
            "Medium descending ticket",
            "medium");
        TicketResponse urgentTicket = await CreateTicketAsync(
            client,
            "Urgent descending ticket",
            "urgent");
        TicketResponse lowTicket = await CreateTicketAsync(
            client,
            "Low descending ticket",
            "low");
        TicketResponse highTicket = await CreateTicketAsync(
            client,
            "High descending ticket",
            "high");

        HttpResponseMessage response = await client.GetAsync(
            "/tickets?sortBy=priority&sortDirection=desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketListEnvelope result = await ReadListAsync(response);

        Assert.Equal(
            [urgentTicket.Id, highTicket.Id, mediumTicket.Id, lowTicket.Id],
            result.Items.Select(ticket => ticket.Id));
    }

    /// <summary>
    /// Verifies deterministic ordering remains stable across page boundaries.
    /// </summary>
    [Fact]
    public async Task Priority_sorting_should_remain_stable_across_pages()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "StablePaging");
        SetBearerToken(client, customer.AccessToken);

        for (int index = 1; index <= 5; index++)
        {
            await CreateTicketAsync(
                client,
                $"Stable priority ticket {index}",
                "medium");
        }

        TicketListEnvelope complete = await GetListAsync(
            client,
            "/tickets?page=1&pageSize=10&sortBy=priority&sortDirection=asc");
        TicketListEnvelope firstPage = await GetListAsync(
            client,
            "/tickets?page=1&pageSize=2&sortBy=priority&sortDirection=asc");
        TicketListEnvelope secondPage = await GetListAsync(
            client,
            "/tickets?page=2&pageSize=2&sortBy=priority&sortDirection=asc");
        TicketListEnvelope thirdPage = await GetListAsync(
            client,
            "/tickets?page=3&pageSize=2&sortBy=priority&sortDirection=asc");

        Guid[] pagedIds = firstPage.Items
            .Concat(secondPage.Items)
            .Concat(thirdPage.Items)
            .Select(ticket => ticket.Id)
            .ToArray();

        Assert.Equal(
            complete.Items.Select(ticket => ticket.Id),
            pagedIds);
        Assert.Equal(5, pagedIds.Distinct().Count());
    }

    /// <summary>
    /// Verifies mutually exclusive assignee filters return Problem Details.
    /// </summary>
    [Fact]
    public async Task Assignee_and_unassigned_filters_should_not_be_combined()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);
        SetBearerToken(client, admin.AccessToken);

        HttpResponseMessage response = await client.GetAsync(
            $"/tickets?assigneeId={Guid.NewGuid()}&unassigned=true");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Verifies unsupported or malformed query values return Problem Details.
    /// </summary>
    [Theory]
    [InlineData("status=not_a_status")]
    [InlineData("status=1")]
    [InlineData("priority=not_a_priority")]
    [InlineData("priority=1")]
    [InlineData("sortBy=title")]
    [InlineData("sortDirection=sideways")]
    [InlineData("requesterId=not-a-guid")]
    [InlineData("assigneeId=not-a-guid")]
    [InlineData("unassigned=not-a-boolean")]
    public async Task Invalid_list_query_should_return_problem_details(
        string query)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(client, "InvalidQuery");
        SetBearerToken(client, customer.AccessToken);

        HttpResponseMessage response = await client.GetAsync(
            $"/tickets?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Verifies a valid but unknown identity filter returns an empty page.
    /// </summary>
    [Fact]
    public async Task Unknown_requester_filter_should_return_an_empty_page()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);
        SetBearerToken(client, admin.AccessToken);

        TicketListEnvelope result = await GetListAsync(
            client,
            $"/tickets?requesterId={Guid.NewGuid()}");

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    /// <summary>
    /// Verifies unassigned=false leaves assignment visibility unfiltered.
    /// </summary>
    [Fact]
    public async Task Unassigned_false_should_not_filter_tickets()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse customer = await RegisterAsync(
            client,
            "UnassignedFalse");
        SetBearerToken(client, customer.AccessToken);

        await CreateTicketAsync(client, "First unfiltered ticket");
        await CreateTicketAsync(client, "Second unfiltered ticket");

        TicketListEnvelope result = await GetListAsync(
            client,
            "/tickets?unassigned=false");

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
    }

    /// <summary>
    /// Registers a unique Customer through the public authentication API.
    /// </summary>
    private static async Task<AuthResponse> RegisterAsync(
        HttpClient client,
        string lastName)
    {
        var request = new RegisterRequest(
            "Query",
            lastName,
            $"query-{Guid.NewGuid():N}@example.com",
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
    /// Creates a Ticket through the public Ticket API.
    /// </summary>
    private static async Task<TicketResponse> CreateTicketAsync(
        HttpClient client,
        string title,
        string? priority = null)
    {
        var request = new
        {
            title,
            description = "Ticket query integration test description.",
            priority
        };

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
    /// Provisions an Agent through the public Admin endpoint.
    /// </summary>
    private static async Task<AgentCredentials> CreateAgentAsync(
        HttpClient client)
    {
        const string password = "AgentPass!";
        string email = $"query-agent-{Guid.NewGuid():N}@example.com";

        var request = new CreateAgentRequest(
            "Query",
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
    /// Assigns one Ticket through the public assignment endpoint.
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
    /// Changes one Ticket status through the public lifecycle endpoint.
    /// </summary>
    private static async Task ChangeStatusAsync(
        HttpClient client,
        Guid ticketId,
        string status)
    {
        HttpResponseMessage response =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticketId}/status",
                new { status });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Adds a public Comment so the Ticket receives a newer update timestamp.
    /// </summary>
    private static async Task AddCommentAsync(
        HttpClient client,
        Guid ticketId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticketId}/comments",
            new AddTicketCommentRequest("Sorting timestamp update."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Reads one required Ticket collection response.
    /// </summary>
    private static async Task<TicketListEnvelope> ReadListAsync(
        HttpResponseMessage response)
    {
        TicketListEnvelope? result =
            await response.Content.ReadFromJsonAsync<TicketListEnvelope>();

        return result
            ?? throw new InvalidOperationException(
                "Ticket list response body was empty.");
    }

    /// <summary>
    /// Executes a successful Ticket-list request and reads its response body.
    /// </summary>
    private static async Task<TicketListEnvelope> GetListAsync(
        HttpClient client,
        string requestUri)
    {
        HttpResponseMessage response = await client.GetAsync(requestUri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadListAsync(response);
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

    private sealed record TicketListItem(
        Guid Id,
        string Title,
        string Priority,
        string Status,
        Guid RequesterId,
        Guid? AssigneeId,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    private sealed record AgentCredentials(
        Guid Id,
        string Email,
        string Password);
}
