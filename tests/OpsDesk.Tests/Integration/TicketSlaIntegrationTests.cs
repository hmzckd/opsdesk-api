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
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Domain.Enums;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class TicketSlaIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private readonly OpsDeskApiFactory _factory;

    public TicketSlaIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies create, details, and list endpoints use the same SLA snapshot.
    /// </summary>
    [Fact]
    public async Task Urgent_ticket_should_report_breach_after_24_hours()
    {
        DateTimeOffset createdAtUtc = new(
            2026,
            9,
            11,
            12,
            0,
            0,
            TimeSpan.Zero);

        var clock = new MutableTimeProvider(createdAtUtc);

        using WebApplicationFactory<Program> timedFactory =
            _factory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                }));

        using HttpClient client = timedFactory.CreateClient();

        AuthResponse admin = await LoginAsAdminAsync(client);
        _factory.VerifyAccount(admin.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                admin.AccessToken);

        HttpResponseMessage createResponse =
            await client.PostAsJsonAsync(
                "/tickets",
                new CreateTicketRequest(
                    "Production database is unavailable",
                    "Customer operations cannot continue.",
                    TicketPriority.Urgent),
                JsonOptions);

        string createResponseBody =
            await createResponse.Content.ReadAsStringAsync();

        Assert.True(
            createResponse.StatusCode == HttpStatusCode.Created,
            $"Expected Created but received " +
            $"{createResponse.StatusCode}. Response: " +
            createResponseBody);

        TicketResponse createdTicket =
            await createResponse.Content
                .ReadFromJsonAsync<TicketResponse>(JsonOptions)
            ?? throw new InvalidOperationException(
                "Ticket response body was empty.");

        Assert.Equal(createdAtUtc.UtcDateTime, createdTicket.CreatedAtUtc);
        Assert.Equal(
            createdAtUtc.AddHours(24).UtcDateTime,
            createdTicket.SlaDeadlineUtc);
        Assert.False(createdTicket.IsSlaBreached);

        clock.Advance(TimeSpan.FromHours(24).Add(TimeSpan.FromSeconds(1)));

        TicketResponse ticketDetails =
            await client.GetFromJsonAsync<TicketResponse>(
                $"/tickets/{createdTicket.Id}",
                JsonOptions)
            ?? throw new InvalidOperationException(
                "Ticket details body was empty.");

        Assert.Equal(
            createdTicket.SlaDeadlineUtc,
            ticketDetails.SlaDeadlineUtc);
        Assert.True(ticketDetails.IsSlaBreached);

        TicketListItemResponse? listedTicket = null;
        int page = 1;

        while (listedTicket is null)
        {
            TicketListEnvelope ticketPage =
                await client.GetFromJsonAsync<TicketListEnvelope>(
                    $"/tickets?page={page}&pageSize=100",
                    JsonOptions)
                ?? throw new InvalidOperationException(
                    "Ticket list body was empty.");

            listedTicket = ticketPage.Items.SingleOrDefault(
                ticket => ticket.Id == createdTicket.Id);

            if (!ticketPage.HasNextPage)
            {
                break;
            }

            page++;
        }

        Assert.NotNull(listedTicket);
        Assert.Equal(
            createdTicket.SlaDeadlineUtc,
            listedTicket.SlaDeadlineUtc);
        Assert.True(listedTicket.IsSlaBreached);
    }

    /// <summary>
    /// Verifies a client-supplied deadline cannot replace the server calculation.
    /// </summary>
    [Fact]
    public async Task Client_should_not_override_ticket_sla_deadline()
    {
        DateTimeOffset createdAtUtc = new(
            2026,
            9,
            11,
            12,
            0,
            0,
            TimeSpan.Zero);
        var clock = new MutableTimeProvider(createdAtUtc);

        using WebApplicationFactory<Program> timedFactory =
            _factory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                }));
        using HttpClient client = timedFactory.CreateClient();
        AuthResponse admin = await LoginAsAdminAsync(client);
        _factory.VerifyAccount(admin.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        DateTime attemptedDeadlineUtc =
            createdAtUtc.AddYears(10).UtcDateTime;

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/tickets",
            new
            {
                title = "Client deadline override attempt",
                description = "The API must calculate its own SLA deadline.",
                priority = "urgent",
                slaDeadlineUtc = attemptedDeadlineUtc
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        TicketResponse ticket = await response.Content
            .ReadFromJsonAsync<TicketResponse>(JsonOptions)
            ?? throw new InvalidOperationException(
                "Ticket response body was empty.");

        Assert.Equal(
            createdAtUtc.AddHours(24).UtcDateTime,
            ticket.SlaDeadlineUtc);
        Assert.NotEqual(attemptedDeadlineUtc, ticket.SlaDeadlineUtc);
    }

    /// <summary>
    /// Verifies a persisted policy edit does not recalculate an existing Ticket.
    /// </summary>
    [Fact]
    public async Task Existing_ticket_should_keep_deadline_after_policy_update()
    {
        using HttpClient client = _factory.CreateClient();
        AuthResponse admin = await LoginAsAdminAsync(client);
        _factory.VerifyAccount(admin.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/tickets",
            new CreateTicketRequest(
                "Persisted SLA snapshot",
                "Existing tickets must keep their original deadline.",
                TicketPriority.High),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        TicketResponse createdTicket = await createResponse.Content
            .ReadFromJsonAsync<TicketResponse>(JsonOptions)
            ?? throw new InvalidOperationException(
                "Ticket response body was empty.");

        Guid slaPolicyId;
        int originalDurationMinutes;

        await using (AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope())
        {
            OpsDeskDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<OpsDeskDbContext>();
            slaPolicyId = await dbContext.Tickets
                .Where(ticket => ticket.Id == createdTicket.Id)
                .Select(ticket => ticket.SlaPolicyId)
                .SingleAsync();
            originalDurationMinutes = await dbContext.SlaPolicies
                .Where(policy => policy.Id == slaPolicyId)
                .Select(policy => policy.ResolutionDurationMinutes)
                .SingleAsync();

            await dbContext.SlaPolicies
                .Where(policy => policy.Id == slaPolicyId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    policy => policy.ResolutionDurationMinutes,
                    originalDurationMinutes + 60));
        }

        try
        {
            TicketResponse existingTicket = await client
                .GetFromJsonAsync<TicketResponse>(
                    $"/tickets/{createdTicket.Id}",
                    JsonOptions)
                ?? throw new InvalidOperationException(
                    "Ticket details body was empty.");

            Assert.Equal(
                createdTicket.SlaDeadlineUtc,
                existingTicket.SlaDeadlineUtc,
                TimeSpan.FromMilliseconds(1));
        }
        finally
        {
            await using AsyncServiceScope scope =
                _factory.Services.CreateAsyncScope();
            OpsDeskDbContext dbContext = scope.ServiceProvider
                .GetRequiredService<OpsDeskDbContext>();

            await dbContext.SlaPolicies
                .Where(policy => policy.Id == slaPolicyId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    policy => policy.ResolutionDurationMinutes,
                    originalDurationMinutes));
        }
    }

    /// <summary>
    /// Authenticates the seeded Admin used by this SLA HTTP scenario.
    /// </summary>
    private static async Task<AuthResponse> LoginAsAdminAsync(
        HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(
                OpsDeskApiFactory.AdminEmail,
                OpsDeskApiFactory.AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException(
                "Authentication response body was empty.");
    }

    /// <summary>
    /// Creates JSON settings that match the API's enum representation.
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
        IReadOnlyList<TicketListItemResponse> Items,
        bool HasNextPage);

    private sealed class MutableTimeProvider(
        DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        /// <summary>
        /// Returns the deterministic UTC time used by the test application.
        /// </summary>
        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        /// <summary>
        /// Moves the deterministic test clock forward by a positive duration.
        /// </summary>
        public void Advance(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration),
                    duration,
                    "Test time must move forward.");
            }

            _utcNow = _utcNow.Add(duration);
        }
    }
}
