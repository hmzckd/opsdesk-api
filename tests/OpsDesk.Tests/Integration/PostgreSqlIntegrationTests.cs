using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Common.Exceptions;
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
    public async Task Initial_migration_should_be_applied_to_postgresql()
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
