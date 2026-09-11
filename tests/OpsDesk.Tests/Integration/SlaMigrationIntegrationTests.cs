using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using OpsDesk.Domain.Entities;
using OpsDesk.Infrastructure.Persistence;
using OpsDesk.Infrastructure.Persistence.Configurations;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class SlaMigrationIntegrationTests
{
    private const string PreviousMigration =
        "20260910104635_AddEmailVerificationQueue";

    private readonly OpsDeskApiFactory _factory;

    public SlaMigrationIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies the SLA migration backfills an existing Medium Ticket.
    /// </summary>
    [Fact]
    public async Task Migration_should_backfill_existing_ticket_sla()
    {
        string databaseName =
            $"opsdesk_sla_backfill_{Guid.NewGuid():N}";

        var administrationConnectionString =
            new NpgsqlConnectionStringBuilder(
                _factory.ConnectionString)
            {
                Database = "postgres"
            };

        await using (var administrationConnection =
            new NpgsqlConnection(
                administrationConnectionString.ConnectionString))
        {
            await administrationConnection.OpenAsync();

            await using var createDatabaseCommand =
                new NpgsqlCommand(
                    $"CREATE DATABASE \"{databaseName}\"",
                    administrationConnection);

            await createDatabaseCommand.ExecuteNonQueryAsync();
        }

        var testConnectionString =
            new NpgsqlConnectionStringBuilder(
                _factory.ConnectionString)
            {
                Database = databaseName
            };

        var options = new DbContextOptionsBuilder<OpsDeskDbContext>()
            .UseNpgsql(testConnectionString.ConnectionString)
            .Options;

        await using var dbContext = new OpsDeskDbContext(options);
        IMigrator migrator =
            dbContext.Database.GetService<IMigrator>();

        await migrator.MigrateAsync(PreviousMigration);

        Guid requesterId = Guid.NewGuid();
        Guid ticketId = Guid.NewGuid();
        DateTime createdAtUtc = new(
            2026,
            9,
            1,
            8,
            30,
            0,
            DateTimeKind.Utc);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO users
                (id, first_name, last_name, email, password_hash, role,
                 created_at_utc, email_verified_at_utc, auth_version)
            VALUES
                ({requesterId}, 'Migration', 'Requester',
                 'migration-requester@example.com', 'test-hash', 'Customer',
                 {createdAtUtc}, {createdAtUtc}, 0);
            """);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO tickets
                (id, title, description, priority, status, requester_id,
                 assignee_id, created_at_utc, updated_at_utc,
                 resolved_at_utc, closed_at_utc, concurrency_token)
            VALUES
                ({ticketId}, 'Existing Ticket',
                 'Created before SLA columns existed.', 'Medium', 'Open',
                 {requesterId}, NULL, {createdAtUtc}, {createdAtUtc},
                 NULL, NULL, {Guid.NewGuid()});
            """);

        await migrator.MigrateAsync();

        Ticket migratedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(ticket => ticket.Id == ticketId);

        Assert.Equal(
            SlaPolicyConfiguration.MediumPolicyId,
            migratedTicket.SlaPolicyId);
        Assert.Equal(
            createdAtUtc.AddDays(4),
            migratedTicket.SlaDeadlineUtc);
    }
}
