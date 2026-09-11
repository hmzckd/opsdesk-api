using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Infrastructure.Persistence;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using System.Collections.Concurrent;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Tests.Integration;

public sealed class OpsDeskApiFactory :
    WebApplicationFactory<Program>
{
    public const string AdminEmail =
        "admin@opsdesk.test";

    public const string AdminPassword =
        "AdminTest!";

    private readonly string _connectionString;

    public string ConnectionString => _connectionString;

    public ConcurrentQueue<EmailVerificationEmail> SentEmails { get; } = new();
    public ConcurrentQueue<InvitationEmail> SentInvitations { get; } = new();
    public PasswordResetMailbox PasswordResetEmails { get; } = new();

    // Prepares a verified account for ticket tests; auth tests use the real confirmation flow.
    public void VerifyAccount(string accessToken)
    {
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(accessToken);
        Guid id = Guid.Parse(jwt.Claims.Single(claim =>
            claim.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
        using IServiceScope scope = Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();
        var user = database.Users.Single(user => user.Id == id);
        if (!user.IsEmailVerified)
        {
            user.MarkEmailVerified(DateTime.UtcNow);
            database.SaveChanges();
        }
    }

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
                        ["PasswordRecovery:WorkerEnabled"] = "false",
                        ["EmailVerification:WorkerEnabled"] = "false",
                        ["Registration:PublicRegistrationEnabled"] = "true",
                        ["AdminSeed:Enabled"] = "true",
                        ["AdminSeed:FirstName"] = "Test",
                        ["AdminSeed:LastName"] = "Administrator",
                        ["AdminSeed:Email"] = AdminEmail,
                        ["AdminSeed:Password"] = AdminPassword
                    });
            });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPasswordResetEmailSender>();
            services.AddSingleton<IPasswordResetEmailSender>(PasswordResetEmails);
            services.RemoveAll<IInvitationEmailSender>();
            services.AddSingleton<IInvitationEmailSender>(new RecordingInvitationSender(SentInvitations));
            services.RemoveAll<IEmailVerificationEmailSender>();
            services.AddSingleton<IEmailVerificationEmailSender>(
                new RecordingEmailSender(SentEmails));
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

    private sealed class RecordingEmailSender(
        ConcurrentQueue<EmailVerificationEmail> sentEmails)
        : IEmailVerificationEmailSender
    {
        // Captures outgoing email so HTTP tests can inspect delivery without SMTP.
        public Task SendAsync(
            EmailVerificationEmail email,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sentEmails.Enqueue(email);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingInvitationSender(ConcurrentQueue<InvitationEmail> sentEmails)
        : IInvitationEmailSender
    {
        // Captures invitation delivery at the email adapter boundary for HTTP tests.
        public Task SendAsync(InvitationEmail email, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sentEmails.Enqueue(email);
            return Task.CompletedTask;
        }
    }
}
