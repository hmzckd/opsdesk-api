using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class TicketCommentIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private readonly OpsDeskApiFactory _factory;

    public TicketCommentIntegrationTests(OpsDeskApiFixture fixture)
    {
        _factory = fixture.Factory;
    }

    /// <summary>
    /// Verifies the requester can add a normalized public comment.
    /// </summary>
    [Fact]
    public async Task Requester_should_add_comment_to_own_ticket()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticket.Id}/comments",
            new { content = "  The printer is still unavailable.  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using JsonDocument document =
            await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());
        JsonElement comment = document.RootElement;

        Guid commentId = comment.GetProperty("id").GetGuid();

        Assert.NotEqual(Guid.Empty, commentId);
        Assert.Equal(
            ticket.Id,
            comment.GetProperty("ticketId").GetGuid());
        Assert.Equal(
            requester.UserId,
            comment.GetProperty("authorId").GetGuid());
        Assert.Equal(
            "The printer is still unavailable.",
            comment.GetProperty("content").GetString());
        Assert.Equal(
            DateTimeKind.Utc,
            comment.GetProperty("createdAtUtc").GetDateTime().Kind);
        Assert.Equal(
            $"/tickets/{ticket.Id}/comments/{commentId}",
            response.Headers.Location?.OriginalString);

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();
        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        TicketComment persistedComment =
            await dbContext.TicketComments
                .AsNoTracking()
                .SingleAsync(item => item.Id == commentId);
        Ticket persistedTicket = await dbContext.Tickets
            .AsNoTracking()
            .SingleAsync(item => item.Id == ticket.Id);

        Assert.Equal(requester.UserId, persistedComment.AuthorId);
        Assert.Equal(ticket.Id, persistedComment.TicketId);
        Assert.Equal(
            "The printer is still unavailable.",
            persistedComment.Content);
        Assert.Equal(
            persistedComment.CreatedAtUtc,
            persistedTicket.UpdatedAtUtc);
    }

    /// <summary>
    /// Verifies invalid Comment content creates no database record.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidCommentContent))]
    public async Task Invalid_comment_content_should_return_bad_request(
        string? content)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticket.Id}/comments",
            new { content });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountCommentsAsync(ticket.Id));
    }

    /// <summary>
    /// Verifies a Customer cannot discover another requester's Ticket.
    /// </summary>
    [Fact]
    public async Task Customer_commenting_on_another_ticket_should_return_not_found()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse owner = await RegisterCustomerAsync(client);
        SetBearerToken(client, owner.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse otherCustomer = await RegisterCustomerAsync(client);
        SetBearerToken(client, otherCustomer.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticket.Id}/comments",
            new { content = "I should not see this Ticket." });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await CountCommentsAsync(ticket.Id));
    }

    /// <summary>
    /// Verifies support roles can add a public Comment to a visible Ticket.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Admin)]
    public async Task Support_user_should_add_comment(
        UserRole supportRole)
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse supportUser =
            await CreateStaffUserAsync(client, supportRole);
        SetBearerToken(client, supportUser.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticket.Id}/comments",
            new { content = "We are investigating this problem." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using JsonDocument document =
            await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());

        Assert.Equal(
            supportUser.UserId,
            document.RootElement.GetProperty("authorId").GetGuid());
    }

    /// <summary>
    /// Verifies a Closed Ticket remains immutable.
    /// </summary>
    [Fact]
    public async Task Closed_ticket_should_reject_comment()
    {
        using HttpClient client = _factory.CreateClient();

        AuthResponse requester = await RegisterCustomerAsync(client);
        SetBearerToken(client, requester.AccessToken);
        TicketResponse ticket = await CreateTicketAsync(client);

        AuthResponse agent =
            await CreateStaffUserAsync(client, UserRole.Agent);
        SetBearerToken(client, agent.AccessToken);

        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "in_progress")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "resolved")).StatusCode);

        SetBearerToken(client, requester.AccessToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await ChangeStatusAsync(
                client,
                ticket.Id,
                "closed")).StatusCode);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{ticket.Id}/comments",
            new { content = "This should not be persisted." });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await CountCommentsAsync(ticket.Id));
    }

    /// <summary>
    /// Verifies comment creation requires authentication.
    /// </summary>
    [Fact]
    public async Task Anonymous_user_should_not_add_comment()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/tickets/{Guid.NewGuid()}/comments",
            new { content = "Anonymous content." });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Supplies invalid values at the public request seam.
    /// </summary>
    public static TheoryData<string?> InvalidCommentContent =>
        new()
        {
            null,
            string.Empty,
            "   ",
            new string('C', TicketComment.MaximumContentLength + 1)
        };

    /// <summary>
    /// Counts persisted Comments for one Ticket.
    /// </summary>
    private async Task<int> CountCommentsAsync(Guid ticketId)
    {
        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        OpsDeskDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();

        return await dbContext.TicketComments
            .AsNoTracking()
            .CountAsync(comment => comment.TicketId == ticketId);
    }

    /// <summary>
    /// Registers a unique Customer through the public API.
    /// </summary>
    private static async Task<AuthResponse> RegisterCustomerAsync(
        HttpClient client)
    {
        var request = new RegisterRequest(
            "Comment",
            "Requester",
            $"comment-{Guid.NewGuid():N}@example.com",
            "ValidPass!");

        HttpResponseMessage response =
            await client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        AuthResponse? result =
            await response.Content.ReadFromJsonAsync<AuthResponse>();

        return result
            ?? throw new InvalidOperationException(
                "Auth response body was empty.");
    }

    /// <summary>
    /// Creates one Ticket through the public API.
    /// </summary>
    private static async Task<TicketResponse> CreateTicketAsync(
        HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/tickets",
            new CreateTicketRequest(
                "Printer is unavailable",
                "The office printer does not respond."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        TicketResponse? ticket = await response.Content
            .ReadFromJsonAsync<TicketResponse>(JsonOptions);

        return ticket
            ?? throw new InvalidOperationException(
                "Ticket response body was empty.");
    }

    /// <summary>
    /// Creates a support User as test setup and authenticates through the API.
    /// </summary>
    private async Task<AuthResponse> CreateStaffUserAsync(
        HttpClient client,
        UserRole role)
    {
        const string password = "ValidPass!";
        string email =
            $"comment-{role.ToString().ToLowerInvariant()}-" +
            $"{Guid.NewGuid():N}@example.com";

        await using AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope();

        IUserRepository userRepository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IPasswordHasher passwordHasher =
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            FirstName = "Comment",
            LastName = role.ToString(),
            Email = email,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = role
        };

        await userRepository.AddAsync(user);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AuthResponse? result =
            await response.Content.ReadFromJsonAsync<AuthResponse>();

        return result
            ?? throw new InvalidOperationException(
                "Auth response body was empty.");
    }

    /// <summary>
    /// Sends one status-change request through the public endpoint.
    /// </summary>
    private static Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        Guid ticketId,
        string status)
    {
        return client.PatchAsJsonAsync(
            $"/tickets/{ticketId}/status",
            new { status });
    }

    /// <summary>
    /// Applies a JWT to subsequent requests made by the client.
    /// </summary>
    private static void SetBearerToken(
        HttpClient client,
        string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>
    /// Creates JSON options that match the API's enum wire format.
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
}
