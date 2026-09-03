using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OpsDesk.Tests.Integration;

public sealed class OpenApiIntegrationTests
{
    /// <summary>
    /// Verifies Swagger shows practical examples for the Ticket list query.
    /// </summary>
    [Fact]
    public async Task Ticket_collection_operation_should_publish_query_examples()
    {
        using var factory = new OpenApiApiFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response =
            await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using Stream responseStream =
            await response.Content.ReadAsStreamAsync();

        using JsonDocument document =
            await JsonDocument.ParseAsync(responseStream);

        string? description = document.RootElement
            .GetProperty("paths")
            .GetProperty("/tickets")
            .GetProperty("get")
            .GetProperty("description")
            .GetString();

        Assert.NotNull(description);
        Assert.Contains(
            "GET /tickets?page=1&pageSize=20",
            description,
            StringComparison.Ordinal);
        Assert.Contains(
            "GET /tickets?status=open&priority=high",
            description,
            StringComparison.Ordinal);
        Assert.Contains(
            "GET /tickets?unassigned=true",
            description,
            StringComparison.Ordinal);
    }

    private sealed class OpenApiApiFactory :
        WebApplicationFactory<Program>
    {
        /// <summary>
        /// Starts the API without database initialization for documentation tests.
        /// </summary>
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("OpenApiTesting");

            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:DefaultConnection"] =
                                "Host=localhost;Database=unused",
                            ["Jwt:Issuer"] = "OpsDesk.Tests",
                            ["Jwt:Audience"] = "OpsDesk.Tests",
                            ["Jwt:SecretKey"] =
                                "TestSecretKeyForOpsDeskApi1234567890!",
                            ["Jwt:ExpirationMinutes"] = "60",
                            ["AdminSeed:Enabled"] = "false"
                        });
                });
        }
    }
}
