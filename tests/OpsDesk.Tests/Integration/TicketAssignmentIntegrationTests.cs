using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class TicketAssignmentIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private readonly OpsDeskApiFactory _factory;

    public TicketAssignmentIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies an Agent can claim an unassigned Ticket atomically.
    /// </summary>
    [Fact]
    public async Task Agent_should_self_assign_unassigned_ticket()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent =
            await CreateStaffUserAsync(client, UserRole.Agent);
        _factory.VerifyAccount(agent.AccessToken);        SetBearerToken(client, agent.AccessToken);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/tickets/{ticket.Id}/assignee",
            new { assigneeId = agent.UserId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TicketResponse? assignedTicket =
            await response.Content.ReadFromJsonAsync<TicketResponse>(
                JsonOptions);

        Assert.NotNull(assignedTicket);
        Assert.Equal(agent.UserId, assignedTicket.AssigneeId);
        Assert.Equal(TicketStatus.Open, assignedTicket.Status);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);
        TicketAssignmentChange assignmentChange =
            await dbContext.Set<TicketAssignmentChange>()
                .AsNoTracking()
                .SingleAsync(item => item.TicketId == ticket.Id);

        Assert.Equal(agent.UserId, persistedTicket.AssigneeId);
        Assert.Equal(agent.UserId, assignmentChange.ActorId);
        Assert.Null(assignmentChange.PreviousAssigneeId);
        Assert.Equal(agent.UserId, assignmentChange.NewAssigneeId);
        Assert.Equal(
            assignmentChange.CreatedAtUtc,
            persistedTicket.UpdatedAtUtc);
    }

    /// <summary>
    /// Verifies an Agent cannot choose another Agent as the assignee.
    /// </summary>
    [Fact]
    public async Task Agent_assigning_ticket_to_another_agent_should_be_forbidden()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse actor =
            await CreateStaffUserAsync(client, UserRole.Agent);
        AuthResponse target =
            await CreateStaffUserAsync(client, UserRole.Agent);
        _factory.VerifyAccount(actor.AccessToken);        SetBearerToken(client, actor.AccessToken);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/tickets/{ticket.Id}/assignee",
            new { assigneeId = target.UserId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);
        int activityCount = await dbContext.TicketAssignmentChanges
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticket.Id);

        Assert.Null(persistedTicket.AssigneeId);
        Assert.Equal(0, activityCount);
    }

    /// <summary>
    /// Verifies another Agent cannot discover or mutate an owned Ticket.
    /// </summary>
    [Fact]
    public async Task Another_agents_ticket_should_be_hidden_from_agent()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse owner =
            await CreateStaffUserAsync(client, UserRole.Agent);
        _factory.VerifyAccount(owner.AccessToken);        SetBearerToken(client, owner.AccessToken);

        HttpResponseMessage assignResponse =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = owner.UserId });

        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        AuthResponse observer =
            await CreateStaffUserAsync(client, UserRole.Agent);
        _factory.VerifyAccount(observer.AccessToken);        SetBearerToken(client, observer.AccessToken);

        HttpResponseMessage detailsResponse =
            await client.GetAsync($"/tickets/{ticket.Id}");
        HttpResponseMessage commentsResponse =
            await client.GetAsync(
                $"/tickets/{ticket.Id}/comments");
        HttpResponseMessage addCommentResponse =
            await client.PostAsJsonAsync(
                $"/tickets/{ticket.Id}/comments",
                new AddTicketCommentRequest("Not my Ticket"));
        HttpResponseMessage historyResponse =
            await client.GetAsync(
                $"/tickets/{ticket.Id}/status-history");
        HttpResponseMessage statusResponse =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                new { status = "in_progress" });
        HttpResponseMessage assignmentResponse =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = observer.UserId });
        HttpResponseMessage unassignmentResponse =
            await client.DeleteAsync(
                $"/tickets/{ticket.Id}/assignee");

        Assert.Equal(
            HttpStatusCode.NotFound,
            detailsResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            commentsResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            addCommentResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            historyResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            statusResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            assignmentResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            unassignmentResponse.StatusCode);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);
        int activityCount = await dbContext.TicketAssignmentChanges
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticket.Id);

        Assert.Equal(owner.UserId, persistedTicket.AssigneeId);
        Assert.Equal(1, activityCount);
    }

    /// <summary>
    /// Verifies an Agent must claim an unassigned Ticket before writing to it.
    /// </summary>
    [Fact]
    public async Task Agent_should_claim_visible_unassigned_ticket_before_writing()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent =
            await CreateStaffUserAsync(client, UserRole.Agent);
        _factory.VerifyAccount(agent.AccessToken);        SetBearerToken(client, agent.AccessToken);

        HttpResponseMessage detailsBeforeAssignment =
            await client.GetAsync($"/tickets/{ticket.Id}");
        HttpResponseMessage commentBeforeAssignment =
            await client.PostAsJsonAsync(
                $"/tickets/{ticket.Id}/comments",
                new AddTicketCommentRequest("Starting work"));
        HttpResponseMessage statusBeforeAssignment =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                new { status = "in_progress" });

        Assert.Equal(
            HttpStatusCode.OK,
            detailsBeforeAssignment.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            commentBeforeAssignment.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            statusBeforeAssignment.StatusCode);

        HttpResponseMessage assignmentResponse =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = agent.UserId });

        Assert.Equal(
            HttpStatusCode.OK,
            assignmentResponse.StatusCode);

        HttpResponseMessage commentAfterAssignment =
            await client.PostAsJsonAsync(
                $"/tickets/{ticket.Id}/comments",
                new AddTicketCommentRequest("Starting work"));
        HttpResponseMessage statusAfterAssignment =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                new { status = "in_progress" });

        Assert.Equal(
            HttpStatusCode.Created,
            commentAfterAssignment.StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            statusAfterAssignment.StatusCode);
    }

    /// <summary>
    /// Verifies an Admin can assign, reassign, inspect, and unassign a Ticket.
    /// </summary>
    [Fact]
    public async Task Admin_should_manage_ticket_assignment_without_changing_status()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse firstAgent =
            await CreateStaffUserAsync(client, UserRole.Agent);
        AuthResponse secondAgent =
            await CreateStaffUserAsync(client, UserRole.Agent);
        AuthResponse admin =
            await CreateStaffUserAsync(client, UserRole.Admin);
        _factory.VerifyAccount(admin.AccessToken);        SetBearerToken(client, admin.AccessToken);

        HttpResponseMessage firstAssignment =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = firstAgent.UserId });
        HttpResponseMessage secondAssignment =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = secondAgent.UserId });
        HttpResponseMessage detailsResponse =
            await client.GetAsync($"/tickets/{ticket.Id}");
        HttpResponseMessage unassignment =
            await client.DeleteAsync(
                $"/tickets/{ticket.Id}/assignee");

        Assert.Equal(HttpStatusCode.OK, firstAssignment.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondAssignment.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            unassignment.StatusCode);

        TicketResponse? details = await detailsResponse.Content
            .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        Assert.NotNull(details);
        Assert.Equal(secondAgent.UserId, details.AssigneeId);
        Assert.Equal(TicketStatus.Open, details.Status);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);
        List<TicketAssignmentChange> activities =
            await dbContext.TicketAssignmentChanges
                .AsNoTracking()
                .Where(item => item.TicketId == ticket.Id)
                .OrderBy(item => item.CreatedAtUtc)
                .ThenBy(item => item.Id)
                .ToListAsync();

        Assert.Null(persistedTicket.AssigneeId);
        Assert.Equal(TicketStatus.Open, persistedTicket.Status);
        Assert.Equal(3, activities.Count);
        Assert.Null(activities[0].PreviousAssigneeId);
        Assert.Equal(firstAgent.UserId, activities[0].NewAssigneeId);
        Assert.Equal(firstAgent.UserId, activities[1].PreviousAssigneeId);
        Assert.Equal(secondAgent.UserId, activities[1].NewAssigneeId);
        Assert.Equal(secondAgent.UserId, activities[2].PreviousAssigneeId);
        Assert.Null(activities[2].NewAssigneeId);
        Assert.All(
            activities,
            activity => Assert.Equal(admin.UserId, activity.ActorId));
    }

    /// <summary>
    /// Verifies assignment targets must identify an existing Agent.
    /// </summary>
    [Fact]
    public async Task Admin_should_reject_missing_or_non_agent_assignee()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse admin =
            await CreateStaffUserAsync(client, UserRole.Admin);
        _factory.VerifyAccount(admin.AccessToken);        SetBearerToken(client, admin.AccessToken);

        HttpResponseMessage missingUserResponse =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = Guid.NewGuid() });
        HttpResponseMessage customerResponse =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = requester.UserId });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            missingUserResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            customerResponse.StatusCode);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);
        int activityCount = await dbContext.TicketAssignmentChanges
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticket.Id);

        Assert.Null(persistedTicket.AssigneeId);
        Assert.Equal(0, activityCount);
    }

    /// <summary>
    /// Verifies a Customer cannot use assignment management endpoints.
    /// </summary>
    [Fact]
    public async Task Customer_assignment_requests_should_be_forbidden()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent =
            await CreateStaffUserAsync(client, UserRole.Agent);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);

        HttpResponseMessage assignmentResponse =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = agent.UserId });
        HttpResponseMessage unassignmentResponse =
            await client.DeleteAsync(
                $"/tickets/{ticket.Id}/assignee");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            assignmentResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            unassignmentResponse.StatusCode);
    }

    /// <summary>
    /// Verifies repeated assignment requests do not create fake activity.
    /// </summary>
    [Fact]
    public async Task Repeated_assignment_requests_should_be_idempotent()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent =
            await CreateStaffUserAsync(client, UserRole.Agent);
        _factory.VerifyAccount(agent.AccessToken);        SetBearerToken(client, agent.AccessToken);

        HttpResponseMessage firstAssignment =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = agent.UserId });
        AssignmentSnapshot assignedOnce =
            await ReadAssignmentSnapshotAsync(ticket.Id);

        HttpResponseMessage repeatedAssignment =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = agent.UserId });
        AssignmentSnapshot assignedTwice =
            await ReadAssignmentSnapshotAsync(ticket.Id);

        Assert.Equal(HttpStatusCode.OK, firstAssignment.StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            repeatedAssignment.StatusCode);
        Assert.Equal(agent.UserId, assignedTwice.AssigneeId);
        Assert.Equal(
            assignedOnce.UpdatedAtUtc,
            assignedTwice.UpdatedAtUtc);
        Assert.Equal(
            assignedOnce.ConcurrencyToken,
            assignedTwice.ConcurrencyToken);
        Assert.Equal(1, assignedTwice.ActivityCount);

        HttpResponseMessage firstUnassignment =
            await client.DeleteAsync(
                $"/tickets/{ticket.Id}/assignee");
        AssignmentSnapshot unassignedOnce =
            await ReadAssignmentSnapshotAsync(ticket.Id);

        HttpResponseMessage repeatedUnassignment =
            await client.DeleteAsync(
                $"/tickets/{ticket.Id}/assignee");
        AssignmentSnapshot unassignedTwice =
            await ReadAssignmentSnapshotAsync(ticket.Id);

        Assert.Equal(
            HttpStatusCode.NoContent,
            firstUnassignment.StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            repeatedUnassignment.StatusCode);
        Assert.Null(unassignedTwice.AssigneeId);
        Assert.Equal(
            unassignedOnce.UpdatedAtUtc,
            unassignedTwice.UpdatedAtUtc);
        Assert.Equal(
            unassignedOnce.ConcurrencyToken,
            unassignedTwice.ConcurrencyToken);
        Assert.Equal(2, unassignedTwice.ActivityCount);
    }

    /// <summary>
    /// Verifies a Closed Ticket rejects every assignment change.
    /// </summary>
    [Fact]
    public async Task Closed_ticket_assignment_changes_should_conflict()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse assignedAgent =
            await CreateStaffUserAsync(client, UserRole.Agent);
        AuthResponse replacementAgent =
            await CreateStaffUserAsync(client, UserRole.Agent);
        _factory.VerifyAccount(assignedAgent.AccessToken);        SetBearerToken(client, assignedAgent.AccessToken);

        HttpResponseMessage assignmentResponse =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = assignedAgent.UserId });
        HttpResponseMessage startResponse =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                new { status = "in_progress" });
        HttpResponseMessage resolveResponse =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                new { status = "resolved" });

        Assert.Equal(HttpStatusCode.OK, assignmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

        _factory.VerifyAccount(requester.AccessToken);
        SetBearerToken(client, requester.AccessToken);
        HttpResponseMessage closeResponse =
            await client.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                new { status = "closed" });

        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        AuthResponse admin =
            await CreateStaffUserAsync(client, UserRole.Admin);
        _factory.VerifyAccount(admin.AccessToken);        SetBearerToken(client, admin.AccessToken);

        HttpResponseMessage reassignResponse =
            await client.PutAsJsonAsync(
                $"/tickets/{ticket.Id}/assignee",
                new { assigneeId = replacementAgent.UserId });
        HttpResponseMessage unassignResponse =
            await client.DeleteAsync(
                $"/tickets/{ticket.Id}/assignee");

        Assert.Equal(
            HttpStatusCode.Conflict,
            reassignResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            unassignResponse.StatusCode);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);
        int activityCount = await dbContext.TicketAssignmentChanges
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticket.Id);

        Assert.Equal(TicketStatus.Closed, persistedTicket.Status);
        Assert.Equal(assignedAgent.UserId, persistedTicket.AssigneeId);
        Assert.Equal(1, activityCount);
    }

    /// <summary>
    /// Verifies concurrent claims produce one winner and one rolled-back loser.
    /// </summary>
    [Fact]
    public async Task Concurrent_agent_claims_should_have_one_winner()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticketResponse = await CreateTicketAsync(client);

        AuthResponse firstAgent =
            await CreateStaffUserAsync(client, UserRole.Agent);
        AuthResponse secondAgent =
            await CreateStaffUserAsync(client, UserRole.Agent);

        await using AsyncServiceScope firstScope =
            _factory.Services.CreateAsyncScope();
        await using AsyncServiceScope secondScope =
            _factory.Services.CreateAsyncScope();

        ITicketRepository firstRepository =
            firstScope.ServiceProvider
                .GetRequiredService<ITicketRepository>();
        ITicketRepository secondRepository =
            secondScope.ServiceProvider
                .GetRequiredService<ITicketRepository>();

        Ticket firstTicket =
            await firstRepository.GetForAssignmentByAgentAsync(
                ticketResponse.Id,
                firstAgent.UserId) ??
            throw new InvalidOperationException(
                "First assignment query did not find the Ticket.");
        Ticket secondTicket =
            await secondRepository.GetForAssignmentByAgentAsync(
                ticketResponse.Id,
                secondAgent.UserId) ??
            throw new InvalidOperationException(
                "Second assignment query did not find the Ticket.");

        DateTime firstChangedAtUtc = DateTime.UtcNow;
        DateTime secondChangedAtUtc = DateTime.UtcNow;

        Assert.True(
            firstTicket.Assign(
                firstAgent.UserId,
                firstChangedAtUtc));
        Assert.True(
            secondTicket.Assign(
                secondAgent.UserId,
                secondChangedAtUtc));

        await firstRepository.AddAssignmentChangeAsync(
            TicketAssignmentChange.Create(
                firstTicket.Id,
                firstAgent.UserId,
                null,
                firstAgent.UserId,
                firstChangedAtUtc));
        await secondRepository.AddAssignmentChangeAsync(
            TicketAssignmentChange.Create(
                secondTicket.Id,
                secondAgent.UserId,
                null,
                secondAgent.UserId,
                secondChangedAtUtc));

        await firstRepository.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(
            () => secondRepository.SaveChangesAsync());

        await using AsyncServiceScope verificationScope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext verificationContext =
            verificationScope.ServiceProvider
                .GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await verificationContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticketResponse.Id);
        List<TicketAssignmentChange> activities =
            await verificationContext.TicketAssignmentChanges
                .AsNoTracking()
                .Where(item => item.TicketId == ticketResponse.Id)
                .ToListAsync();

        Assert.Equal(firstAgent.UserId, persistedTicket.AssigneeId);
        TicketAssignmentChange activity = Assert.Single(activities);
        Assert.Equal(firstAgent.UserId, activity.ActorId);
        Assert.Equal(firstAgent.UserId, activity.NewAssigneeId);
    }

    /// <summary>
    /// Registers a unique Customer through the public API.
    /// </summary>
    private async Task<AuthResponse> RegisterCustomerAsync(
        HttpClient client)
    {
        var request = new RegisterRequest(
            "Assignment",
            "Requester",
            $"assignment-{Guid.NewGuid():N}@example.com",
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
    /// Creates one Ticket through the public API.
    /// </summary>
    private async Task<TicketResponse> CreateTicketAsync(
        HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/tickets",
            new CreateTicketRequest(
                "Printer is unavailable",
                "The office printer does not respond."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        TicketResponse? ticket = await response.Content
            .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        return ticket
            ?? throw new InvalidOperationException(
                "Ticket response body was empty.");
    }

    /// <summary>
    /// Creates a support User as test setup and authenticates through the API.
    /// </summary>
    private async Task<AuthResponse> CreateStaffUserAsync(
        HttpClient client,
        UserRole role)
    {
        const string password = "ValidPass!";
        string email =
            $"assignment-{role.ToString().ToLowerInvariant()}-" +
            $"{Guid.NewGuid():N}@example.com";

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository userRepository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IPasswordHasher passwordHasher =
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            FirstName = "Assignment",
            LastName = role.ToString(),
            Email = email,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = role
        };

        await userRepository.AddAsync(user);

        HttpResponseMessage response = await client.PostAsJsonAsync(
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
    /// Reads the persisted fields used to verify assignment idempotency.
    /// </summary>
    private async Task<AssignmentSnapshot> ReadAssignmentSnapshotAsync(
        Guid ticketId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        Ticket ticket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticketId);
        int activityCount = await dbContext.TicketAssignmentChanges
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticketId);

        return new AssignmentSnapshot(
            ticket.AssigneeId,
            ticket.UpdatedAtUtc,
            ticket.ConcurrencyToken,
            activityCount);
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

    private sealed record AssignmentSnapshot(
        Guid? AssigneeId,
        DateTime UpdatedAtUtc,
        Guid ConcurrencyToken,
        int ActivityCount);
}
