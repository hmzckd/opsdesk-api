using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Application.Audit.DTOs;
using OpsDesk.Application.Audit.Interfaces;
using OpsDesk.Application.Audit.Queries;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Common.Pagination;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Invitations.Models;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuditLogIntegrationTests(OpsDeskApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    /// <summary>
    /// Checks Ticket creation and status changes from the Admin HTTP read seam.
    /// </summary>
    [Fact]
    public async Task Ticket_creation_and_status_change_should_be_audited()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        AuthResponse admin = await LoginAsAdminAsync(client);
        fixture.Factory.VerifyAccount(admin.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        using HttpResponseMessage changed = await client.PatchAsJsonAsync(
            $"/tickets/{ticket.Id}/status",
            new ChangeTicketStatusRequest(TicketStatus.InProgress),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        PagedResponse<AuditLogResponse> page = await GetPageAsync(
            client, $"?targetType=ticket&targetId={ticket.Id}");
        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, entry => Assert.Equal(admin.UserId, entry.ActorId));
        Assert.Contains(page.Items, entry =>
            entry.Action == AuditAction.TicketCreated &&
            entry.TargetId == ticket.Id);
        AuditLogResponse transition = Assert.Single(page.Items,
            entry => entry.Action == AuditAction.TicketStatusChanged);
        Assert.Equal(TicketStatus.Open, transition.PreviousStatus);
        Assert.Equal(TicketStatus.InProgress, transition.NewStatus);
    }

    /// <summary>
    /// Covers both assignment directions and the requester's reopen action.
    /// </summary>
    [Fact]
    public async Task Assignment_and_reopen_should_record_safe_transitions()
    {
        using HttpClient adminClient = fixture.Factory.CreateClient();
        AuthResponse admin = await LoginAsAdminAsync(adminClient);
        fixture.Factory.VerifyAccount(admin.AccessToken);
        using HttpResponseMessage agentResponse = await adminClient.PostAsJsonAsync(
            "/admin/agents", new
            {
                firstName = "Audit", lastName = "Agent",
                email = $"audit-agent-{Guid.NewGuid():N}@example.com",
                password = "AgentPass!"
            });
        Assert.Equal(HttpStatusCode.Created, agentResponse.StatusCode);
        using JsonDocument agentBody = JsonDocument.Parse(
            await agentResponse.Content.ReadAsStringAsync());
        Guid agentId = agentBody.RootElement.GetProperty("id").GetGuid();

        using HttpClient customerClient = fixture.Factory.CreateClient();
        using HttpResponseMessage registration = await customerClient.PostAsJsonAsync(
            "/auth/register", new RegisterRequest("Audit", "Customer",
                $"audit-customer-{Guid.NewGuid():N}@example.com", "CustomerPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        AuthResponse customer = await registration.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Customer registration was empty.");
        fixture.Factory.VerifyAccount(customer.AccessToken);
        customerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", customer.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(customerClient);

        using HttpResponseMessage assigned = await adminClient.PutAsJsonAsync(
            $"/tickets/{ticket.Id}/assignee", new { assigneeId = agentId });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        using HttpResponseMessage unassigned = await adminClient.DeleteAsync(
            $"/tickets/{ticket.Id}/assignee");
        Assert.Equal(HttpStatusCode.NoContent, unassigned.StatusCode);

        foreach (TicketStatus status in new[]
            { TicketStatus.InProgress, TicketStatus.Resolved })
        {
            using HttpResponseMessage changed = await adminClient.PatchAsJsonAsync(
                $"/tickets/{ticket.Id}/status",
                new ChangeTicketStatusRequest(status), JsonOptions);
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        }

        using HttpResponseMessage reopened = await customerClient.PostAsJsonAsync(
            $"/tickets/{ticket.Id}/reopen",
            new ReopenTicketRequest("The issue is not resolved."));
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);

        PagedResponse<AuditLogResponse> page = await GetPageAsync(
            adminClient, $"?targetType=ticket&targetId={ticket.Id}&pageSize=20");
        Assert.Equal(6, page.TotalCount);
        AuditLogResponse[] assignments = page.Items
            .Where(entry => entry.Action == AuditAction.TicketAssigneeChanged)
            .OrderBy(entry => entry.OccurredAtUtc).ToArray();
        Assert.Equal(2, assignments.Length);
        Assert.Null(assignments[0].PreviousAssigneeId);
        Assert.Equal(agentId, assignments[0].NewAssigneeId);
        Assert.Equal(agentId, assignments[1].PreviousAssigneeId);
        Assert.Null(assignments[1].NewAssigneeId);
        AuditLogResponse reopenEntry = Assert.Single(page.Items,
            entry => entry.Action == AuditAction.TicketStatusChanged &&
                entry.ActorId == customer.UserId);
        Assert.Equal(TicketStatus.Resolved, reopenEntry.PreviousStatus);
        Assert.Equal(TicketStatus.InProgress, reopenEntry.NewStatus);
    }

    /// <summary>
    /// Proves Admin identity operations never expose credentials in audit output.
    /// </summary>
    [Fact]
    public async Task Agent_and_invitation_creation_should_be_audited_without_secrets()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        AuthResponse admin = await LoginAsAdminAsync(client);
        string agentEmail = $"agent-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage agentResponse = await client.PostAsJsonAsync(
            "/admin/agents", new
            {
                firstName = "Audit", lastName = "Agent",
                email = agentEmail, password = "SecretAgentPass!",
                actorId = Guid.NewGuid()
            });
        Assert.Equal(HttpStatusCode.Created, agentResponse.StatusCode);
        using JsonDocument agentBody = JsonDocument.Parse(
            await agentResponse.Content.ReadAsStringAsync());
        Guid agentId = agentBody.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage invitationResponse = await client.PostAsJsonAsync(
            "/admin/invitations", new
            {
                email = $"invite-{Guid.NewGuid():N}@example.com",
                role = "agent", actorId = Guid.NewGuid()
            });
        Assert.Equal(HttpStatusCode.Created, invitationResponse.StatusCode);
        using JsonDocument invitationBody = JsonDocument.Parse(
            await invitationResponse.Content.ReadAsStringAsync());
        Guid invitationId = invitationBody.RootElement.GetProperty("id").GetGuid();

        PagedResponse<AuditLogResponse> agents = await GetPageAsync(
            client, $"?action=agent_created&targetType=user&targetId={agentId}");
        Assert.Equal(admin.UserId, Assert.Single(agents.Items).ActorId);
        PagedResponse<AuditLogResponse> invitations = await GetPageAsync(
            client, $"?action=invitation_created&targetType=invitation&targetId={invitationId}");
        Assert.Equal(admin.UserId, Assert.Single(invitations.Items).ActorId);

        foreach (string path in new[]
        {
            $"/admin/audit-logs?targetType=user&targetId={agentId}",
            $"/admin/audit-logs?targetType=invitation&targetId={invitationId}"
        })
        {
            using HttpResponseMessage auditResponse = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
            string body = await auditResponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain("SecretAgentPass!", body);
            Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("email", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(agentEmail, body);
        }
    }

    /// <summary>
    /// A failed email send records the invitation and its actual revocation.
    /// </summary>
    [Fact]
    public async Task Failed_invitation_delivery_should_audit_creation_and_revocation()
    {
        using WebApplicationFactory<Program> failingFactory =
            fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInvitationEmailSender>();
                services.AddSingleton<IInvitationEmailSender, FailingInvitationSender>();
            }));
        using HttpClient client = failingFactory.CreateClient();
        AuthResponse admin = await LoginAsAdminAsync(client);
        string email = $"failed-invite-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/admin/invitations", new { email, role = "agent" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        Guid invitationId;
        using (IServiceScope scope = failingFactory.Services.CreateScope())
        {
            OpsDeskDbContext database = scope.ServiceProvider
                .GetRequiredService<OpsDeskDbContext>();
            UserInvitation invitation = await database.UserInvitations
                .AsNoTracking().SingleAsync(row => row.Email == email);
            Assert.NotNull(invitation.RevokedAtUtc);
            invitationId = invitation.Id;
        }

        PagedResponse<AuditLogResponse> page = await GetPageAsync(
            client, $"?targetType=invitation&targetId={invitationId}");
        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, entry => Assert.Equal(admin.UserId, entry.ActorId));
        Assert.Contains(page.Items, entry => entry.Action == AuditAction.InvitationCreated);
        Assert.Contains(page.Items, entry => entry.Action == AuditAction.InvitationRevoked);
    }

    /// <summary>
    /// Checks Admin-only access, filter validation, and stable equal-time paging.
    /// </summary>
    [Fact]
    public async Task Admin_only_query_should_filter_and_page_stably()
    {
        using HttpClient anonymous = fixture.Factory.CreateClient();
        using HttpResponseMessage unauthorized = await anonymous.GetAsync(
            "/admin/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using HttpResponseMessage registered = await anonymous.PostAsJsonAsync(
            "/auth/register", new RegisterRequest("Audit", "Viewer",
                $"viewer-{Guid.NewGuid():N}@example.com", "ViewerPass!"));
        AuthResponse customer = await registered.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Registration was empty.");
        anonymous.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", customer.AccessToken);
        using HttpResponseMessage forbidden = await anonymous.GetAsync(
            "/admin/audit-logs");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using HttpClient agentClient = fixture.Factory.CreateClient();
        await LoginAsAdminAsync(agentClient);
        string agentEmail = $"audit-reader-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage createdAgent = await agentClient.PostAsJsonAsync(
            "/admin/agents", new
            {
                firstName = "Audit", lastName = "Reader",
                email = agentEmail, password = "AgentPass!"
            });
        Assert.Equal(HttpStatusCode.Created, createdAgent.StatusCode);
        using HttpResponseMessage agentLogin = await agentClient.PostAsJsonAsync(
            "/auth/login", new LoginRequest(agentEmail, "AgentPass!"));
        AuthResponse agent = await agentLogin.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Agent login was empty.");
        agentClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agent.AccessToken);
        using HttpResponseMessage agentDenied = await agentClient.GetAsync(
            "/admin/audit-logs");
        Assert.Equal(HttpStatusCode.Forbidden, agentDenied.StatusCode);

        var clock = new FixedAuditClock(new DateTimeOffset(
            2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        using WebApplicationFactory<Program> timedFactory =
            fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            }));
        using HttpClient client = timedFactory.CreateClient();
        AuthResponse admin = await LoginAsAdminAsync(client);
        fixture.Factory.VerifyAccount(admin.AccessToken);
        for (int index = 0; index < 5; index++)
        {
            await CreateTicketAsync(client);
        }

        string filter = $"?action=ticket_created&targetType=ticket&actorId={admin.UserId}" +
            "&fromUtc=2030-01-02T00:00:00Z&toUtc=2030-01-03T00:00:00Z&pageSize=1";
        PagedResponse<AuditLogResponse>[] pages = await Task.WhenAll(
            Enumerable.Range(1, 5)
                .Select(page => GetPageAsync(client, filter + $"&page={page}")));
        PagedResponse<AuditLogResponse> firstAgain = await GetPageAsync(client, filter);
        Assert.Equal(5, pages[0].TotalCount);
        Assert.True(pages[0].HasNextPage);
        Assert.Equal(pages[0].Items[0].Id, firstAgain.Items[0].Id);
        Guid[] actualOrder = pages.SelectMany(page => page.Items)
            .Select(entry => entry.Id).ToArray();
        Assert.Equal(5, actualOrder.Distinct().Count());
        Guid[] expectedOrder = actualOrder
            .OrderByDescending(id => id.ToString("N"), StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedOrder, actualOrder);

        using HttpResponseMessage badAction = await client.GetAsync(
            "/admin/audit-logs?action=login_failed");
        Assert.Equal(HttpStatusCode.BadRequest, badAction.StatusCode);
        using HttpResponseMessage badRange = await client.GetAsync(
            "/admin/audit-logs?fromUtc=2030-01-03T00:00:00Z&toUtc=2030-01-02T00:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, badRange.StatusCode);
        using HttpResponseMessage missingZone = await client.GetAsync(
            "/admin/audit-logs?fromUtc=2030-01-02T00:00:00");
        Assert.Equal(HttpStatusCode.BadRequest, missingZone.StatusCode);
        using HttpResponseMessage missingTargetType = await client.GetAsync(
            $"/admin/audit-logs?targetId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.BadRequest, missingTargetType.StatusCode);
    }

    /// <summary>
    /// Repeated or rejected ownership requests must not invent a change.
    /// </summary>
    [Fact]
    public async Task Unchanged_or_invalid_assignment_should_not_add_audit_entry()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        AuthResponse admin = await LoginAsAdminAsync(client);
        fixture.Factory.VerifyAccount(admin.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);
        using HttpResponseMessage agentResponse = await client.PostAsJsonAsync(
            "/admin/agents", new
            {
                firstName = "Audit", lastName = "Owner",
                email = $"audit-owner-{Guid.NewGuid():N}@example.com",
                password = "AgentPass!"
            });
        Assert.Equal(HttpStatusCode.Created, agentResponse.StatusCode);
        using JsonDocument agentBody = JsonDocument.Parse(
            await agentResponse.Content.ReadAsStringAsync());
        Guid agentId = agentBody.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage first = await client.PutAsJsonAsync(
            $"/tickets/{ticket.Id}/assignee", new { assigneeId = agentId });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using HttpResponseMessage repeated = await client.PutAsJsonAsync(
            $"/tickets/{ticket.Id}/assignee", new { assigneeId = agentId });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        using HttpResponseMessage invalid = await client.PutAsJsonAsync(
            $"/tickets/{ticket.Id}/assignee", new { assigneeId = admin.UserId });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        PagedResponse<AuditLogResponse> page = await GetPageAsync(client,
            $"?action=ticket_assignee_changed&targetType=ticket&targetId={ticket.Id}");
        Assert.Single(page.Items);
        Assert.Equal(1, page.TotalCount);
    }

    /// <summary>
    /// Forces audit persistence to fail and checks the Ticket insert rolls back.
    /// </summary>
    [Fact]
    public async Task Audit_write_failure_should_roll_back_ticket_creation()
    {
        using WebApplicationFactory<Program> failingFactory =
            fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditLogRepository>();
                services.AddScoped<IAuditLogRepository, InvalidActorAuditRepository>();
            }));
        using HttpClient client = failingFactory.CreateClient();
        AuthResponse admin = await LoginAsAdminAsync(client);
        fixture.Factory.VerifyAccount(admin.AccessToken);
        string title = $"Rollback check {Guid.NewGuid():N}";

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/tickets", new CreateTicketRequest(
                title, "Audit insert is intentionally invalid."), JsonOptions);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using IServiceScope scope = failingFactory.Services.CreateScope();
        OpsDeskDbContext database = scope.ServiceProvider
            .GetRequiredService<OpsDeskDbContext>();
        Assert.False(await database.Tickets.AsNoTracking()
            .AnyAsync(ticket => ticket.Title == title));
    }

    private static async Task<AuthResponse> LoginAsAdminAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(
                OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuthResponse admin = await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Admin login was empty.");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        return admin;
    }

    private static async Task<TicketResponse> CreateTicketAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/tickets", new CreateTicketRequest(
                $"Audit ticket {Guid.NewGuid():N}", "A support request for audit coverage."),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<TicketResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Ticket creation was empty.");
    }

    private static async Task<PagedResponse<AuditLogResponse>> GetPageAsync(
        HttpClient client, string query)
    {
        using HttpResponseMessage response = await client.GetAsync(
            "/admin/audit-logs" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content
            .ReadFromJsonAsync<PagedResponse<AuditLogResponse>>(JsonOptions)
            ?? throw new InvalidOperationException("Audit page was empty.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        return options;
    }

    private sealed class FixedAuditClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FailingInvitationSender : IInvitationEmailSender
    {
        public Task SendAsync(InvitationEmail email,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated SMTP failure.");
    }

    private sealed class InvalidActorAuditRepository(OpsDeskDbContext database)
        : IAuditLogRepository
    {
        public void Stage(AuditLog entry)
        {
            database.AuditLogs.Add(AuditLog.ForTicketCreated(
                entry.TargetId, Guid.NewGuid(), entry.OccurredAtUtc));
        }

        public Task<PagedResponse<AuditLogResponse>> GetPageAsync(
            AuditLogQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This test only covers writes.");
    }
}
