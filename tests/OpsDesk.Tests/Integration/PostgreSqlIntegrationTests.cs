using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;
using OpsDesk.Infrastructure.Persistence;
using OpsDesk.Infrastructure.Persistence.Configurations;
using OpsDesk.Tests.Tickets;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class PostgreSqlIntegrationTests
{
    private readonly OpsDeskApiFactory _factory;

    public PostgreSqlIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies PostgreSQL contains every migration known by the EF Core model.
    /// </summary>
    [Fact]
    public async Task All_known_migrations_should_be_applied_to_postgresql()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        OpsDeskDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OpsDeskDbContext>();

        Assert.Equal(
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            dbContext.Database.ProviderName);

        string[] knownMigrations =
            dbContext.Database
                .GetMigrations()
            .ToArray();

        string[] appliedMigrations =
            (await dbContext.Database
                .GetAppliedMigrationsAsync())
            .ToArray();

        Assert.NotEmpty(knownMigrations);
        Assert.Equal(knownMigrations, appliedMigrations);
    }

    [Fact]
    public async Task Duplicate_email_constraint_should_return_conflict()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository repository = scope.ServiceProvider
            .GetRequiredService<IUserRepository>();

        string email =
            $"constraint-{Guid.NewGuid():N}@example.com";

        await repository.AddAsync(CreateUser(email));

        await Assert.ThrowsAsync<ConflictException>(() =>
            repository.AddAsync(CreateUser(email)));
    }

    /// <summary>
    /// Verifies Ticket persistence against the real PostgreSQL provider.
    /// </summary>
    [Fact]
    public async Task Ticket_should_be_persisted_to_postgresql()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository userRepository =
            scope.ServiceProvider
                .GetRequiredService<IUserRepository>();

        ITicketRepository ticketRepository =
            scope.ServiceProvider
                .GetRequiredService<ITicketRepository>();

        OpsDeskDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<OpsDeskDbContext>();

        User requester = CreateUser(
            $"requester-{Guid.NewGuid():N}@example.com");

        await userRepository.AddAsync(requester);

        Ticket ticket = TicketTestFactory.Create(
            requester.Id,
            "Database integration test",
            "This Ticket must be stored in PostgreSQL.",
            policyId: SlaPolicyConfiguration.MediumPolicyId);

        await ticketRepository.AddAsync(ticket);

        Ticket persistedTicket =
            await dbContext.Tickets
                .AsNoTracking()
                .SingleAsync(item => item.Id == ticket.Id);

        Assert.Equal(requester.Id, persistedTicket.RequesterId);
        Assert.Equal(ticket.Title, persistedTicket.Title);
    }

    /// <summary>
    /// Verifies PostgreSQL rejects a Ticket with an unknown requester.
    /// </summary>
    [Fact]
    public async Task Ticket_requester_foreign_key_should_be_enforced()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        ITicketRepository repository =
            scope.ServiceProvider
                .GetRequiredService<ITicketRepository>();

        Ticket ticket = TicketTestFactory.Create(
            Guid.NewGuid(),
            "Unknown requester",
            "The requester does not exist in the users table.",
            policyId: SlaPolicyConfiguration.MediumPolicyId);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repository.AddAsync(ticket));
    }

    /// <summary>
    /// Verifies a failed history insert rolls back the Ticket update too.
    /// </summary>
    [Fact]
    public async Task Status_change_should_be_atomic_in_postgresql()
    {
        Guid ticketId;

        await using (AsyncServiceScope writeScope =
            _factory.Services.CreateAsyncScope())
        {
            IUserRepository userRepository = writeScope.ServiceProvider
                .GetRequiredService<IUserRepository>();
            ITicketRepository ticketRepository = writeScope.ServiceProvider
                .GetRequiredService<ITicketRepository>();

            User requester = CreateUser(
                $"atomic-{Guid.NewGuid():N}@example.com");
            await userRepository.AddAsync(requester);

            Ticket ticket = TicketTestFactory.Create(
                requester.Id,
                "Atomic status change",
                "The Ticket update must roll back with its event.",
                policyId: SlaPolicyConfiguration.MediumPolicyId);
            await ticketRepository.AddAsync(ticket);
            ticketId = ticket.Id;

            Ticket trackedTicket =
                await ticketRepository.GetForUpdateAsync(ticket.Id)
                ?? throw new InvalidOperationException(
                    "Ticket was not found for update.");

            DateTime changedAtUtc = DateTime.UtcNow;
            trackedTicket.ChangeStatus(
                TicketStatus.InProgress,
                changedAtUtc);

            TicketStatusChange invalidStatusChange =
                TicketStatusChange.Create(
                    ticket.Id,
                    Guid.NewGuid(),
                    TicketStatus.Open,
                    TicketStatus.InProgress,
                    changedAtUtc);

            await ticketRepository.AddStatusChangeAsync(
                invalidStatusChange);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                ticketRepository.SaveChangesAsync());
        }

        await using AsyncServiceScope readScope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext = readScope.ServiceProvider
            .GetRequiredService<OpsDeskDbContext>();

        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticketId);
        int eventCount = await dbContext.TicketStatusChanges
            .AsNoTracking()
            .CountAsync(item => item.TicketId == ticketId);

        Assert.Equal(TicketStatus.Open, persistedTicket.Status);
        Assert.Equal(0, eventCount);
    }

    private static User CreateUser(string email)
    {
        return new User
        {
            FirstName = "Database",
            LastName = "Test",
            Email = email,
            PasswordHash = "not-a-real-password-hash"
        };
    }
}
