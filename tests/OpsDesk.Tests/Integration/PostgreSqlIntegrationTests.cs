using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Domain.Entities;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class PostgreSqlIntegrationTests
{
    private readonly OpsDeskApiFactory _factory;

    public PostgreSqlIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Expected_migrations_should_be_applied_to_postgresql()
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        OpsDeskDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OpsDeskDbContext>();

        Assert.Equal(
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            dbContext.Database.ProviderName);

        string[] appliedMigrations =
            (await dbContext.Database
                .GetAppliedMigrationsAsync())
            .ToArray();

        Assert.Contains(
            appliedMigrations,
            migration => migration.EndsWith(
                "_InitialCreate",
                StringComparison.Ordinal));

        Assert.Contains(
            appliedMigrations,
            migration => migration.EndsWith(
                "_AddTickets",
                StringComparison.Ordinal));
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

        Ticket ticket = Ticket.Create(
            requester.Id,
            "Database integration test",
            "This Ticket must be stored in PostgreSQL.");

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

        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            "Unknown requester",
            "The requester does not exist in the users table.");

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repository.AddAsync(ticket));
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
