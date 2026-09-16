using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpsDesk.Api.Authorization;
using OpsDesk.Api.Configuration;
using OpsDesk.Api.RateLimiting;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpsDesk.Application.Agents.Interfaces;
using OpsDesk.Application.Agents.Services;
using OpsDesk.Application.Audit.Interfaces;
using OpsDesk.Application.Audit.Services;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Services;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Application.Tickets.Services;
using OpsDesk.Api.ErrorHandling;
using OpsDesk.Infrastructure;
using OpsDesk.Infrastructure.Seed;
using OpsDesk.Infrastructure.Authentication;
using OpsDesk.Infrastructure.Persistence;
using OpsDesk.Application.Authorization;
using OpsDesk.Application.Sla.Interfaces;
using OpsDesk.Application.Sla.Services;
using OpsDesk.Domain.Enums;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRegistrationConfiguration();
builder.Services.AddSsoConfiguration();
builder.Services.AddSlaBreachMonitoring();

builder.Services.AddScoped<OpsDesk.Application.Invitations.Interfaces.IInvitationService,
    OpsDesk.Application.Invitations.Services.InvitationService>();


builder.Services.AddHealthChecks();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.SnakeCaseLower,
                allowIntegerValues: false));
    });
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<TimeProvider>(
    TimeProvider.System);
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthSessionService, AuthSessionService>();
builder.Services.AddScoped<IExternalSignInService, ExternalSignInService>();
builder.Services.AddScoped<IPasswordRecoveryService, PasswordRecoveryService>();
builder.Services.AddScoped<JwtSessionValidationEvents>();
builder.Services.AddHostedService<OpsDesk.Api.BackgroundServices.PasswordRecoveryWorker>();
builder.Services.AddHostedService<OpsDesk.Api.BackgroundServices.EmailVerificationWorker>();
builder.Services.AddPasswordRecoveryRateLimiting();
builder.Services.AddScoped<
    IEmailVerificationService,
    EmailVerificationService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ISlaBreachService, SlaBreachService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();


builder.Services.AddSingleton<IPasswordValidator, PasswordValidator>();
builder.Services.AddSingleton<IEmailValidator, EmailValidator>();

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(
        JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>(
        (options, jwtOptions) =>
        {
            JwtSettings jwtSettings = jwtOptions.Value;

            options.MapInboundClaims = false;
            options.EventsType = typeof(JwtSessionValidationEvents);

            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(
                                jwtSettings.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
        });

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(
        AuthorizationPolicies.VerifiedEmail,
        policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new VerifiedEmailRequirement()))
    .AddPolicy(
        AuthorizationPolicies.AdminOnly,
        policy => policy.RequireRole(
            UserRole.Admin.ToString()))
    .AddPolicy(
        AuthorizationPolicies.AgentOrAdmin,
        policy => policy.RequireRole(
            UserRole.Admin.ToString(),
            UserRole.Agent.ToString()));

builder.Services.AddScoped<IAuthorizationHandler, VerifiedEmailHandler>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OpsDesk API",
        Version = "v1",
        Description = "Internal support and operations management API."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter the JWT access token."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                "Bearer",
                document)] = []
        });

    options.OperationFilter<TicketListExamplesOperationFilter>();
});

var app = builder.Build();

// Validate registration before startup database preparation, not only when the first request arrives.
_ = app.Services.GetRequiredService<IOptions<OpsDesk.Application.Auth.Models.RegistrationSettings>>().Value;
_ = app.Services.GetRequiredService<IOptions<SsoSettings>>().Value;

using (IServiceScope scope = app.Services.CreateScope())
{
    OpsDeskDbContext dbContext = scope.ServiceProvider
        .GetRequiredService<OpsDeskDbContext>();

    if (app.Environment.IsDevelopment() ||
        app.Environment.IsEnvironment("Testing"))
    {
        await dbContext.Database.MigrateAsync();
    }

    AdminUserSeeder adminUserSeeder =
        scope.ServiceProvider
            .GetRequiredService<AdminUserSeeder>();

    await adminUserSeeder.SeedAsync();
}

app.UseExceptionHandler();

app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "OpsDesk API v1");

    options.DocumentTitle = "OpsDesk API";
    options.EnablePersistAuthorization();
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            service = "OpsDesk API"
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
})
    .WithName("Health")
    .WithSummary("Returns API health status.")
    .WithDescription(
        "Confirms that the API process is running.");

app.Run();

public partial class Program;

internal sealed class TicketListExamplesOperationFilter :
    IOperationFilter
{
    /// <summary>
    /// Adds practical query examples only to the Ticket collection operation.
    /// </summary>
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        bool isTicketListOperation =
            string.Equals(
                context.ApiDescription.HttpMethod,
                HttpMethods.Get,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                context.ApiDescription.RelativePath,
                "tickets",
                StringComparison.OrdinalIgnoreCase);

        if (!isTicketListOperation)
        {
            return;
        }

        operation.Description = """
            Example queries:

            GET /tickets?page=1&pageSize=20

            GET /tickets?status=open&priority=high

            GET /tickets?unassigned=true
            """;
    }
}
