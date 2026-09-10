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
public sealed class TicketReopenIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private readonly OpsDeskApiFactory _factory;

    public TicketReopenIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies the requester reopens a resolved Ticket with one public reason.
    /// </summary>
    [Fact]
    public async Task Requester_should_reopen_resolved_ticket_with_public_reason()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);
        _factory.VerifyAccount(admin.AccessToken);        SetBearerToken(client, admin.AccessToken);
        await ChangeStatusAsync(client, ticket.Id, "in_progress");
        await ChangeStatusAsync(client, ticket.Id, "resolved");

        _factory.VerifyAccount(requester.AccessToken);
        SetBearerToken(client, requester.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticket.Id}/reopen",
            new
            {
                reason = "  The proposed solution did not fix the problem.  "
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document =
            await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());

        JsonElement reopenedTicket =
            document.RootElement.GetProperty("ticket");
        JsonElement reasonComment =
            document.RootElement.GetProperty("reasonComment");

        Assert.Equal(
            ticket.Id,
            reopenedTicket.GetProperty("id").GetGuid());
        Assert.Equal(
            "in_progress",
            reopenedTicket.GetProperty("status").GetString());
        Assert.Equal(
            JsonValueKind.Null,
            reopenedTicket.GetProperty("resolvedAtUtc").ValueKind);

        Assert.Equal(
            ticket.Id,
            reasonComment.GetProperty("ticketId").GetGuid());
        Assert.Equal(
            requester.UserId,
            reasonComment.GetProperty("authorId").GetGuid());
        Assert.Equal(
            "The proposed solution did not fix the problem.",
            reasonComment.GetProperty("content").GetString());

        TicketResponse persistedTicket =
            await GetTicketAsync(client, ticket.Id);

        Assert.Equal(TicketStatus.InProgress, persistedTicket.Status);
        Assert.Null(persistedTicket.ResolvedAtUtc);

        TicketCommentResponse[] comments =
            await GetCommentsAsync(client, ticket.Id);

        TicketCommentResponse persistedReason =
            Assert.Single(comments);

        Assert.Equal(requester.UserId, persistedReason.AuthorId);
        Assert.Equal(
            "The proposed solution did not fix the problem.",
            persistedReason.Content);

        TicketStatusChangeResponse[] history =
            await GetStatusHistoryAsync(client, ticket.Id);

        Assert.Equal(3, history.Length);

        TicketStatusChangeResponse reopenEvent = history[^1];

        Assert.Equal(requester.UserId, reopenEvent.ActorId);
        Assert.Equal(TicketStatus.Resolved, reopenEvent.PreviousStatus);
        Assert.Equal(TicketStatus.InProgress, reopenEvent.NewStatus);
    }

    /// <summary>
    /// Verifies the requester cannot bypass the required reopen reason.
    /// </summary>
    [Fact]
    public async Task Requester_status_patch_should_not_bypass_reopen_reason()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);
        _factory.VerifyAccount(admin.AccessToken);        SetBearerToken(client, admin.AccessToken);
        await ChangeStatusAsync(client, ticket.Id, "in_progress");
        await ChangeStatusAsync(client, ticket.Id, "resolved");

        _factory.VerifyAccount(requester.AccessToken);
        SetBearerToken(client, requester.AccessToken);

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/tickets/{ticket.Id}/status",
            new { status = "in_progress" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        TicketResponse persistedTicket =
            await GetTicketAsync(client, ticket.Id);
        TicketCommentResponse[] comments =
            await GetCommentsAsync(client, ticket.Id);
        TicketStatusChangeResponse[] history =
            await GetStatusHistoryAsync(client, ticket.Id);

        Assert.Equal(TicketStatus.Resolved, persistedTicket.Status);
        Assert.NotNull(persistedTicket.ResolvedAtUtc);
        Assert.Empty(comments);
        Assert.Equal(2, history.Length);
    }

    /// <summary>
    /// Verifies support Users cannot reopen through the generic status endpoint.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public async Task Support_status_patch_should_not_reopen_resolved_ticket(
        UserRole supportRole)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse supportUser =
            await CreateStaffUserAsync(client, supportRole);
        _factory.VerifyAccount(supportUser.AccessToken);        SetBearerToken(client, supportUser.AccessToken);

        if (supportRole == UserRole.Agent)
        {
            await AssignTicketAsync(
                client,
                ticket.Id,
                supportUser.UserId);
        }

        await ChangeStatusAsync(client, ticket.Id, "in_progress");
        await ChangeStatusAsync(client, ticket.Id, "resolved");

        HttpResponseMessage response = await client.PatchAsJsonAsync(
            $"/tickets/{ticket.Id}/status",
            new { status = "in_progress" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        TicketResponse persistedTicket =
            await GetTicketAsync(client, ticket.Id);
        TicketStatusChangeResponse[] history =
            await GetStatusHistoryAsync(client, ticket.Id);

        Assert.Equal(TicketStatus.Resolved, persistedTicket.Status);
        Assert.NotNull(persistedTicket.ResolvedAtUtc);
        Assert.Equal(2, history.Length);
    }

    /// <summary>
    /// Verifies support Users cannot call the requester-only reopen endpoint.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public async Task Support_user_reopen_request_should_be_forbidden(
        UserRole supportRole)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse supportUser =
            await CreateStaffUserAsync(client, supportRole);
        _factory.VerifyAccount(supportUser.AccessToken);        SetBearerToken(client, supportUser.AccessToken);

        if (supportRole == UserRole.Agent)
        {
            await AssignTicketAsync(
                client,
                ticket.Id,
                supportUser.UserId);
        }

        await ChangeStatusAsync(client, ticket.Id, "in_progress");
        await ChangeStatusAsync(client, ticket.Id, "resolved");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticket.Id}/reopen",
            new { reason = "Support cannot reopen this Ticket." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        TicketResponse persistedTicket =
            await GetTicketAsync(client, ticket.Id);
        TicketCommentResponse[] comments =
            await GetCommentsAsync(client, ticket.Id);
        TicketStatusChangeResponse[] history =
            await GetStatusHistoryAsync(client, ticket.Id);

        Assert.Equal(TicketStatus.Resolved, persistedTicket.Status);
        Assert.Empty(comments);
        Assert.Equal(2, history.Length);
    }

    /// <summary>
    /// Verifies a Customer cannot discover another requester's Ticket.
    /// </summary>
    [Fact]
    public async Task Customer_reopening_another_ticket_should_return_not_found()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse owner = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(owner.AccessToken);        SetBearerToken(client, owner.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse otherCustomer = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(otherCustomer.AccessToken);        SetBearerToken(client, otherCustomer.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticket.Id}/reopen",
            new { reason = "This should remain hidden." });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Verifies only a resolved Ticket can enter the reopen workflow.
    /// </summary>
    [Fact]
    public async Task Reopening_non_resolved_ticket_should_return_conflict()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticket.Id}/reopen",
            new { reason = "The Ticket is not resolved yet." });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        TicketResponse persistedTicket =
            await GetTicketAsync(client, ticket.Id);
        TicketCommentResponse[] comments =
            await GetCommentsAsync(client, ticket.Id);
        TicketStatusChangeResponse[] history =
            await GetStatusHistoryAsync(client, ticket.Id);

        Assert.Equal(TicketStatus.Open, persistedTicket.Status);
        Assert.Empty(comments);
        Assert.Empty(history);
    }

    /// <summary>
    /// Verifies invalid reasons are rejected before any state change.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidReopenReasons))]
    public async Task Invalid_reopen_reason_should_return_bad_request(
        string? reason)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);
        _factory.VerifyAccount(admin.AccessToken);        SetBearerToken(client, admin.AccessToken);
        await ChangeStatusAsync(client, ticket.Id, "in_progress");
        await ChangeStatusAsync(client, ticket.Id, "resolved");

        _factory.VerifyAccount(requester.AccessToken);
        SetBearerToken(client, requester.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticket.Id}/reopen",
            new { reason });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        TicketResponse persistedTicket =
            await GetTicketAsync(client, ticket.Id);
        TicketCommentResponse[] comments =
            await GetCommentsAsync(client, ticket.Id);
        TicketStatusChangeResponse[] history =
            await GetStatusHistoryAsync(client, ticket.Id);

        Assert.Equal(TicketStatus.Resolved, persistedTicket.Status);
        Assert.Empty(comments);
        Assert.Equal(2, history.Length);
    }

    /// <summary>
    /// Verifies the reopen endpoint requires a valid JWT.
    /// </summary>
    [Fact]
    public async Task Anonymous_reopen_request_should_return_unauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{Guid.NewGuid()}/reopen",
            new { reason = "Anonymous reopen attempt." });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Verifies a failed Comment insert rolls back every reopen change.
    /// </summary>
    [Fact]
    public async Task Reopen_persistence_failure_should_roll_back_all_changes()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        _factory.VerifyAccount(requester.AccessToken);        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse admin = await LoginAsync(
            client,
            OpsDeskApiFactory.AdminEmail,
            OpsDeskApiFactory.AdminPassword);
        _factory.VerifyAccount(admin.AccessToken);        SetBearerToken(client, admin.AccessToken);
        await ChangeStatusAsync(client, ticket.Id, "in_progress");
        await ChangeStatusAsync(client, ticket.Id, "resolved");

        const string constraintName =
            "ck_ticket_comments_reopen_atomic_test";
        const string rejectedReason = "Force atomic rollback.";

        await ExecuteDatabaseCommandAsync(
            $"ALTER TABLE ticket_comments DROP CONSTRAINT IF EXISTS " +
            $"{constraintName}");

        try
        {
            await ExecuteDatabaseCommandAsync(
                $"ALTER TABLE ticket_comments ADD CONSTRAINT " +
                $"{constraintName} CHECK " +
                $"(content <> '{rejectedReason}')");

            _factory.VerifyAccount(requester.AccessToken);
            SetBearerToken(client, requester.AccessToken);

            HttpResponseMessage response = await client.PostAsJsonAsync(
                $"/tickets/{ticket.Id}/reopen",
                new { reason = rejectedReason });

            Assert.Equal(
                HttpStatusCode.InternalServerError,
                response.StatusCode);

            TicketResponse persistedTicket =
                await GetTicketAsync(client, ticket.Id);
            TicketCommentResponse[] comments =
                await GetCommentsAsync(client, ticket.Id);
            TicketStatusChangeResponse[] history =
                await GetStatusHistoryAsync(client, ticket.Id);

            Assert.Equal(TicketStatus.Resolved, persistedTicket.Status);
            Assert.NotNull(persistedTicket.ResolvedAtUtc);
            Assert.Empty(comments);
            Assert.Equal(2, history.Length);
        }
        finally
        {
            await ExecuteDatabaseCommandAsync(
                $"ALTER TABLE ticket_comments DROP CONSTRAINT IF EXISTS " +
                $"{constraintName}");
        }
    }

    public static TheoryData<string?> InvalidReopenReasons =>
        new()
        {
            null,
            string.Empty,
            "   ",
            new string('x', TicketComment.MaximumContentLength + 1)
        };

    /// <summary>
    /// Registers a unique Customer through the public API.
    /// </summary>
    private async Task<AuthResponse> RegisterCustomerAsync(
        HttpClient client)
    {
        var request = new RegisterRequest(
            "Reopen",
            "Requester",
            $"reopen-{Guid.NewGuid():N}@example.com",
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
            "Resolution did not work",
            "The reported dashboard problem remains unresolved.");

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
    /// Authenticates an existing User through the public API.
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
            await response.Content.ReadFromJsonAsync<AuthResponse>();

        return result
            ?? throw new InvalidOperationException(
                "Auth response body was empty.");
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
            $"reopen-{normalizedRole}-{Guid.NewGuid():N}@example.com";

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository userRepository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IPasswordHasher passwordHasher =
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            FirstName = "Reopen",
            LastName = role.ToString(),
            Email = email,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = role
        };

        await userRepository.AddAsync(user);

        return await LoginAsync(client, email, password);
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
    /// Executes one test-only PostgreSQL schema command.
    /// </summary>
    private async Task ExecuteDatabaseCommandAsync(string commandText)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(commandText);
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
    /// Reads the current Ticket state through the public API.
    /// </summary>
    private async Task<TicketResponse> GetTicketAsync(
        HttpClient client,
        Guid ticketId)
    {
        TicketResponse? result = await client.GetFromJsonAsync<
            TicketResponse>(
                $"/tickets/{ticketId}",
                JsonOptions);

        return result
            ?? throw new InvalidOperationException(
                "Ticket response body was empty.");
    }

    /// <summary>
    /// Reads the public Ticket conversation through the public API.
    /// </summary>
    private async Task<TicketCommentResponse[]> GetCommentsAsync(
        HttpClient client,
        Guid ticketId)
    {
        TicketCommentResponse[]? result =
            await client.GetFromJsonAsync<TicketCommentResponse[]>(
                $"/tickets/{ticketId}/comments");

        return result
            ?? throw new InvalidOperationException(
                "Comments response body was empty.");
    }

    /// <summary>
    /// Reads the Ticket status history through the public API.
    /// </summary>
    private async Task<TicketStatusChangeResponse[]>
        GetStatusHistoryAsync(
            HttpClient client,
            Guid ticketId)
    {
        TicketStatusChangeResponse[]? result =
            await client.GetFromJsonAsync<TicketStatusChangeResponse[]>(
                $"/tickets/{ticketId}/status-history",
                JsonOptions);

        return result
            ?? throw new InvalidOperationException(
                "Status-history response body was empty.");
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
