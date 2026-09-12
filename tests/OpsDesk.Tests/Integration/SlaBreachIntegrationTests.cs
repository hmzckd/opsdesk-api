using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Sla.Interfaces;
using OpsDesk.Application.Sla.Services;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;
using OpsDesk.Infrastructure.Persistence;
using OpsDesk.Infrastructure.Persistence.Configurations;
using OpsDesk.Tests.Tickets;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class SlaBreachIntegrationTests
{
    private readonly OpsDeskApiFactory _factory;

    public SlaBreachIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies the hosted worker records an overdue Ticket for the public activity API.
    /// </summary>
    [Fact]
    public async Task Enabled_worker_should_publish_one_system_breach_activity()
    {
        DateTime nowUtc = DateTime.UtcNow;
        Guid ticketId = await CreateOverdueTicketAsync(
            _factory,
            nowUtc,
            "Hosted worker breach detection");

        using WebApplicationFactory<Program> workerFactory =
            _factory.WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["SlaBreach:WorkerEnabled"] = "true",
                            ["SlaBreach:PollIntervalSeconds"] = "1"
                        })));
        using HttpClient client = workerFactory.CreateClient();

        HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(
                OpsDeskApiFactory.AdminEmail,
                OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        AuthResponse admin = await loginResponse.Content
            .ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException(
                "Authentication response body was empty.");
        _factory.VerifyAccount(admin.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                admin.AccessToken);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));
        using var timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(100));

        while (!timeout.IsCancellationRequested)
        {
            HttpResponseMessage response = await client.GetAsync(
                $"/tickets/{ticketId}/activity",
                timeout.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeout.Token),
                cancellationToken: timeout.Token);

            JsonElement[] breaches = document.RootElement
                .EnumerateArray()
                .Where(item =>
                    item.GetProperty("type").GetString() ==
                    "sla_breached")
                .ToArray();

            if (breaches.Length == 1)
            {
                Assert.Equal(
                    JsonValueKind.Null,
                    breaches[0].GetProperty("actor").ValueKind);
                return;
            }

            Assert.Empty(breaches);
            await timer.WaitForNextTickAsync(timeout.Token);
        }

        Assert.Fail("The hosted worker did not publish an SLA breach.");
    }

    /// <summary>
    /// Verifies one overdue unresolved Ticket produces one durable breach event.
    /// </summary>
    [Fact]
    public async Task Overdue_unresolved_ticket_should_record_one_breach()
    {
        DateTime detectedAtUtc = new(
            2025,
            1,
            10,
            12,
            0,
            0,
            DateTimeKind.Utc);

        var clock = new FixedTimeProvider(detectedAtUtc);

        using WebApplicationFactory<Program> timedFactory =
            _factory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                }));

        await using AsyncServiceScope scope =
            timedFactory.Services.CreateAsyncScope();

        IUserRepository userRepository = scope.ServiceProvider
            .GetRequiredService<IUserRepository>();
        ITicketRepository ticketRepository = scope.ServiceProvider
            .GetRequiredService<ITicketRepository>();
        ISlaBreachService breachService = scope.ServiceProvider
            .GetRequiredService<ISlaBreachService>();
        OpsDeskDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OpsDeskDbContext>();

        var requester = new User
        {
            FirstName = "SLA",
            LastName = "Requester",
            Email = $"sla-breach-{Guid.NewGuid():N}@example.com",
            PasswordHash = "not-a-real-password-hash"
        };

        await userRepository.AddAsync(requester);

        Ticket ticket = TicketTestFactory.Create(
            requester.Id,
            "Overdue database incident",
            "This unresolved Ticket has exceeded its SLA deadline.",
            policyId: SlaPolicyConfiguration.MediumPolicyId,
            createdAtUtc: detectedAtUtc.AddDays(-5));

        await ticketRepository.AddAsync(ticket);

        int recordedCount =
            await breachService.DetectAndRecordAsync(100);

        TicketSlaBreach breach = await dbContext.TicketSlaBreaches
            .AsNoTracking()
            .SingleAsync(item => item.TicketId == ticket.Id);

        Assert.Equal(1, recordedCount);
        Assert.Equal(ticket.SlaPolicyId, breach.SlaPolicyId);
        Assert.Equal(ticket.SlaDeadlineUtc, breach.SlaDeadlineUtc);
        Assert.Equal(detectedAtUtc, breach.DetectedAtUtc);
    }

    /// <summary>
    /// Verifies repeated detection remains idempotent for one Ticket.
    /// </summary>
    [Fact]
    public async Task Repeated_detection_should_not_duplicate_a_breach()
    {
        DateTime detectedAtUtc = new(
            2025,
            2,
            10,
            12,
            0,
            0,
            DateTimeKind.Utc);

        using WebApplicationFactory<Program> timedFactory =
            CreateTimedFactory(detectedAtUtc);

        Guid ticketId = await CreateOverdueTicketAsync(
            timedFactory,
            detectedAtUtc,
            "Repeated detection");

        await using AsyncServiceScope scope =
            timedFactory.Services.CreateAsyncScope();
        ISlaBreachService service = scope.ServiceProvider
            .GetRequiredService<ISlaBreachService>();
        OpsDeskDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OpsDeskDbContext>();

        int firstCount = await service.DetectAndRecordAsync(100);
        int secondCount = await service.DetectAndRecordAsync(100);

        int storedCount = await dbContext.TicketSlaBreaches
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticketId);

        Assert.Equal(1, firstCount);
        Assert.Equal(0, secondCount);
        Assert.Equal(1, storedCount);
    }

    /// <summary>
    /// Verifies concurrent detection passes cannot duplicate one breach.
    /// </summary>
    [Fact]
    public async Task Concurrent_detection_should_record_one_breach()
    {
        DateTime detectedAtUtc = new(
            2025,
            3,
            10,
            12,
            0,
            0,
            DateTimeKind.Utc);

        using WebApplicationFactory<Program> timedFactory =
            CreateTimedFactory(detectedAtUtc);

        Guid ticketId = await CreateOverdueTicketAsync(
            timedFactory,
            detectedAtUtc,
            "Concurrent detection");

        await using AsyncServiceScope firstScope =
            timedFactory.Services.CreateAsyncScope();
        await using AsyncServiceScope secondScope =
            timedFactory.Services.CreateAsyncScope();

        ISlaBreachService firstService = firstScope.ServiceProvider
            .GetRequiredService<ISlaBreachService>();
        ISlaBreachService secondService = secondScope.ServiceProvider
            .GetRequiredService<ISlaBreachService>();

        int[] recordedCounts = await Task.WhenAll(
            firstService.DetectAndRecordAsync(100),
            secondService.DetectAndRecordAsync(100));

        await using AsyncServiceScope readScope =
            timedFactory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext = readScope.ServiceProvider
            .GetRequiredService<OpsDeskDbContext>();

        int storedCount = await dbContext.TicketSlaBreaches
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticketId);

        Assert.Equal(1, recordedCounts.Sum());
        Assert.Equal(1, storedCount);
    }

    /// <summary>
    /// Verifies a Ticket resolved after its deadline is still recorded later.
    /// </summary>
    [Fact]
    public async Task Late_resolved_ticket_should_record_a_breach()
    {
        DateTime detectedAtUtc = new(
            2025,
            4,
            10,
            12,
            0,
            0,
            DateTimeKind.Utc);

        using WebApplicationFactory<Program> timedFactory =
            CreateTimedFactory(detectedAtUtc);

        Ticket ticket = await CreateTicketAsync(
            timedFactory,
            detectedAtUtc,
            "Late resolution",
            item =>
            {
                item.ChangeStatus(
                    TicketStatus.InProgress,
                    item.CreatedAtUtc.AddDays(1));
                item.ChangeStatus(
                    TicketStatus.Resolved,
                    item.SlaDeadlineUtc.AddSeconds(1));
            });

        await DetectBreachesAsync(timedFactory);

        Assert.True(await BreachExistsAsync(timedFactory, ticket.Id));
    }

    /// <summary>
    /// Verifies resolution exactly at the deadline remains on time.
    /// </summary>
    [Fact]
    public async Task Exact_deadline_resolution_should_not_record_a_breach()
    {
        DateTime detectedAtUtc = new(
            2025,
            5,
            10,
            12,
            0,
            0,
            DateTimeKind.Utc);

        using WebApplicationFactory<Program> timedFactory =
            CreateTimedFactory(detectedAtUtc);

        Ticket ticket = await CreateTicketAsync(
            timedFactory,
            detectedAtUtc,
            "Exact deadline resolution",
            item =>
            {
                item.ChangeStatus(
                    TicketStatus.InProgress,
                    item.CreatedAtUtc.AddDays(1));
                item.ChangeStatus(
                    TicketStatus.Resolved,
                    item.SlaDeadlineUtc);
            });

        await DetectBreachesAsync(timedFactory);

        Assert.False(await BreachExistsAsync(timedFactory, ticket.Id));
    }

    /// <summary>
    /// Verifies reopening restores the original overdue SLA countdown.
    /// </summary>
    [Fact]
    public async Task Reopened_overdue_ticket_should_record_a_breach()
    {
        DateTime detectedAtUtc = new(
            2025,
            6,
            10,
            12,
            0,
            0,
            DateTimeKind.Utc);

        Guid requesterId = Guid.Empty;

        using WebApplicationFactory<Program> timedFactory =
            CreateTimedFactory(detectedAtUtc);

        Ticket ticket = await CreateTicketAsync(
            timedFactory,
            detectedAtUtc,
            "Reopened overdue Ticket",
            item =>
            {
                requesterId = item.RequesterId;
                item.ChangeStatus(
                    TicketStatus.InProgress,
                    item.CreatedAtUtc.AddDays(1));
                item.ChangeStatus(
                    TicketStatus.Resolved,
                    item.SlaDeadlineUtc.AddDays(-1));
                item.Reopen(
                    requesterId,
                    "The reported issue still occurs.",
                    item.SlaDeadlineUtc.AddSeconds(1));
            });

        await DetectBreachesAsync(timedFactory);

        Assert.True(await BreachExistsAsync(timedFactory, ticket.Id));
    }

    /// <summary>
    /// Verifies an on-time resolution and breach scan cannot observe conflicting Ticket states.
    /// </summary>
    [Fact]
    public async Task On_time_resolution_should_not_race_with_breach_detection()
    {
        DateTime detectedAtUtc = new(
            2025,
            7,
            10,
            12,
            0,
            0,
            DateTimeKind.Utc);

        using WebApplicationFactory<Program> timedFactory =
            CreateTimedFactory(detectedAtUtc);

        Ticket ticket = await CreateTicketAsync(
            timedFactory,
            detectedAtUtc,
            "Concurrent on-time resolution",
            configure: null);

        await using AsyncServiceScope statusScope =
            timedFactory.Services.CreateAsyncScope();
        await using AsyncServiceScope breachScope =
            timedFactory.Services.CreateAsyncScope();

        ITicketRepository statusRepository = statusScope.ServiceProvider
            .GetRequiredService<ITicketRepository>();
        ISlaBreachService breachService = breachScope.ServiceProvider
            .GetRequiredService<ISlaBreachService>();

        var ticketLocked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowStatusCommit = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var operationTimeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));
        Task statusTask = statusRepository.ExecuteInWriteTransactionAsync(
            async cancellationToken =>
            {
                Ticket lockedTicket =
                    await statusRepository.GetForUpdateAsync(
                        ticket.Id,
                        cancellationToken)
                    ?? throw new InvalidOperationException(
                        "The test Ticket was not found.");

                lockedTicket.ChangeStatus(
                    TicketStatus.InProgress,
                    lockedTicket.CreatedAtUtc.AddHours(1));
                lockedTicket.ChangeStatus(
                    TicketStatus.Resolved,
                    lockedTicket.SlaDeadlineUtc);

                ticketLocked.SetResult();
                await allowStatusCommit.Task.WaitAsync(cancellationToken);
                await statusRepository.SaveChangesAsync(cancellationToken);

                return true;
            },
            operationTimeout.Token);

        int countWhileResolutionIsPending;

        try
        {
            await ticketLocked.Task.WaitAsync(
                operationTimeout.Token);
            countWhileResolutionIsPending =
                await breachService.DetectAndRecordAsync(
                    100,
                    operationTimeout.Token);
        }
        finally
        {
            allowStatusCommit.TrySetResult();
            await statusTask.WaitAsync(TimeSpan.FromSeconds(10));
        }

        int countAfterResolution =
            await breachService.DetectAndRecordAsync(100);

        Assert.Equal(0, countWhileResolutionIsPending);
        Assert.Equal(0, countAfterResolution);
        Assert.False(await BreachExistsAsync(timedFactory, ticket.Id));
    }

    /// <summary>
    /// Verifies the real status service protects an exact-deadline resolution.
    /// </summary>
    [Fact]
    public async Task Status_service_should_keep_exact_deadline_resolution_on_time()
    {
        DateTime resolvedAtUtc = new(
            2025,
            7,
            11,
            12,
            0,
            0,
            DateTimeKind.Utc);
        using WebApplicationFactory<Program> timedFactory =
            CreateTimedFactory(resolvedAtUtc);
        Ticket ticket = await CreateTicketAsync(
            timedFactory,
            resolvedAtUtc.AddDays(1),
            "Status service exact deadline",
            item => item.ChangeStatus(
                TicketStatus.InProgress,
                item.CreatedAtUtc.AddDays(1)));
        Assert.Equal(resolvedAtUtc, ticket.SlaDeadlineUtc);

        await using (AsyncServiceScope statusScope =
            timedFactory.Services.CreateAsyncScope())
        {
            OpsDeskDbContext database = statusScope.ServiceProvider
                .GetRequiredService<OpsDeskDbContext>();
            Guid adminId = await database.Users
                .Where(user => user.Email == OpsDeskApiFactory.AdminEmail)
                .Select(user => user.Id)
                .SingleAsync();
            ITicketService statusService = statusScope.ServiceProvider
                .GetRequiredService<ITicketService>();

            TicketResponse? response = await statusService.ChangeStatusAsync(
                ticket.Id,
                adminId,
                UserRole.Admin,
                new ChangeTicketStatusRequest(TicketStatus.Resolved));

            Assert.NotNull(response);
            Assert.Equal(resolvedAtUtc, response.ResolvedAtUtc);
        }

        await using (AsyncServiceScope breachScope =
            timedFactory.Services.CreateAsyncScope())
        {
            ISlaBreachRepository repository = breachScope.ServiceProvider
                .GetRequiredService<ISlaBreachRepository>();
            var breachService = new SlaBreachService(
                repository,
                new FixedTimeProvider(resolvedAtUtc.AddSeconds(1)));

            Assert.Equal(0, await breachService.DetectAndRecordAsync(100));
        }

        Assert.False(await BreachExistsAsync(timedFactory, ticket.Id));
    }

    /// <summary>
    /// Creates an API host whose server clock is fixed for one scenario.
    /// </summary>
    private WebApplicationFactory<Program> CreateTimedFactory(
        DateTime detectedAtUtc)
    {
        var clock = new FixedTimeProvider(detectedAtUtc);

        return _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            }));
    }

    /// <summary>
    /// Persists one uniquely named unresolved Ticket whose deadline has passed.
    /// </summary>
    private static async Task<Guid> CreateOverdueTicketAsync(
        WebApplicationFactory<Program> factory,
        DateTime detectedAtUtc,
        string title)
    {
        Ticket ticket = await CreateTicketAsync(
            factory,
            detectedAtUtc,
            title,
            configure: null);

        return ticket.Id;
    }

    /// <summary>
    /// Persists one Ticket after applying optional lifecycle preparation.
    /// </summary>
    private static async Task<Ticket> CreateTicketAsync(
        WebApplicationFactory<Program> factory,
        DateTime detectedAtUtc,
        string title,
        Action<Ticket>? configure)
    {
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();
        IUserRepository userRepository = scope.ServiceProvider
            .GetRequiredService<IUserRepository>();
        ITicketRepository ticketRepository = scope.ServiceProvider
            .GetRequiredService<ITicketRepository>();

        var requester = new User
        {
            FirstName = "SLA",
            LastName = "Requester",
            Email = $"sla-breach-{Guid.NewGuid():N}@example.com",
            PasswordHash = "not-a-real-password-hash"
        };

        await userRepository.AddAsync(requester);

        Ticket ticket = TicketTestFactory.Create(
            requester.Id,
            title,
            "This unresolved Ticket has exceeded its SLA deadline.",
            policyId: SlaPolicyConfiguration.MediumPolicyId,
            createdAtUtc: detectedAtUtc.AddDays(-5));

        configure?.Invoke(ticket);

        await ticketRepository.AddAsync(ticket);

        return ticket;
    }

    /// <summary>
    /// Runs one breach detection pass through the Application seam.
    /// </summary>
    private static async Task DetectBreachesAsync(
        WebApplicationFactory<Program> factory)
    {
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();
        ISlaBreachService service = scope.ServiceProvider
            .GetRequiredService<ISlaBreachService>();

        await service.DetectAndRecordAsync(100);
    }

    /// <summary>
    /// Reads whether one durable breach exists for the selected Ticket.
    /// </summary>
    private static async Task<bool> BreachExistsAsync(
        WebApplicationFactory<Program> factory,
        Guid ticketId)
    {
        await using AsyncServiceScope scope =
            factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OpsDeskDbContext>();

        return await dbContext.TicketSlaBreaches
            .AsNoTracking()
            .AnyAsync(item => item.TicketId == ticketId);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        /// <summary>
        /// Returns the fixed UTC instant used by the breach detection scenario.
        /// </summary>
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(utcNow);
        }
    }
}
