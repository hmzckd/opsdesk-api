using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class EmailFormatIntegrationTests
{
    private const string UnqualifiedEmail = "hamza@gmail";

    private readonly OpsDeskApiFactory _factory;

    public EmailFormatIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies an incomplete email domain is rejected before persistence.
    /// </summary>
    [Fact]
    public async Task Unqualified_email_registration_should_be_rejected_without_creating_user()
    {
        using HttpClient client = _factory.CreateClient();

        var request = new RegisterRequest(
            "Hamza",
            "Customer",
            UnqualifiedEmail,
            "ValidPass!");

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/auth/register",
                request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        ProblemDetails? problemDetails =
            await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problemDetails);
        Assert.Contains(
            "qualified domain",
            problemDetails.Detail ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        OpsDeskDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OpsDeskDbContext>();

        bool userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == UnqualifiedEmail);

        Assert.False(userExists);
    }

    /// <summary>
    /// Verifies qualified addresses are accepted and normalized.
    /// </summary>
    [Theory]
    [InlineData("hamza@gmail.com", "hamza@gmail.com")]
    [InlineData(
        " Normalized.User@Gmail.com ",
        "normalized.user@gmail.com")]
    public async Task Qualified_email_registration_should_be_accepted_and_normalized(
        string email,
        string expectedEmail)
    {
        using HttpClient client = _factory.CreateClient();

        var request = new RegisterRequest(
            "Hamza",
            "Customer",
            email,
            "ValidPass!");

        HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/auth/register",
                request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        AuthResponse? authResponse =
            await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(authResponse);
        Assert.Equal(expectedEmail, authResponse.Email);
    }
}
