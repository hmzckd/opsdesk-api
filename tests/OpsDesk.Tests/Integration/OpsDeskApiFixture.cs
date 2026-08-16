using Testcontainers.PostgreSql;

namespace OpsDesk.Tests.Integration;

public sealed class OpsDeskApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer =
        new PostgreSqlBuilder("postgres:16.4-alpine")
            .WithDatabase("opsdesk_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private OpsDeskApiFactory? _factory;

    public OpsDeskApiFactory Factory =>
        _factory ?? throw new InvalidOperationException(
            "The API test fixture has not been initialized.");

    public async Task InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync();

        _factory = new OpsDeskApiFactory(
            _postgreSqlContainer.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();

        await _postgreSqlContainer.DisposeAsync();
    }
}
