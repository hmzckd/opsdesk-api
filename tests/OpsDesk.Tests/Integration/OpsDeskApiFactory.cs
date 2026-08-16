using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

public sealed class OpsDeskApiFactory :
    WebApplicationFactory<Program>
{
    public const string AdminEmail =
        "admin@opsdesk.test";

    public const string AdminPassword =
        "AdminTest!";

    private readonly string _connectionString;

    public OpsDeskApiFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);

        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            _connectionString,
                        ["Jwt:Issuer"] = "OpsDesk.Tests",
                        ["Jwt:Audience"] = "OpsDesk.Tests",
                        ["Jwt:SecretKey"] =
                            "TestSecretKeyForOpsDeskApi1234567890!",
                        ["Jwt:ExpirationMinutes"] = "60",
                        ["AdminSeed:Enabled"] = "true",
                        ["AdminSeed:FirstName"] = "Test",
                        ["AdminSeed:LastName"] = "Administrator",
                        ["AdminSeed:Email"] = AdminEmail,
                        ["AdminSeed:Password"] = AdminPassword
                    });
            });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<OpsDeskDbContext>();

            services.RemoveAll<
                DbContextOptions<OpsDeskDbContext>>();

            services.RemoveAll<
                IDbContextOptionsConfiguration<
                    OpsDeskDbContext>>();

            services.AddDbContext<OpsDeskDbContext>(
                options => options.UseNpgsql(
                    _connectionString));
        });
    }
}
