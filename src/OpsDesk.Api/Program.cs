using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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
using OpsDesk.Domain.Enums;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);


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
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITicketService, TicketService>();
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
        AuthorizationPolicies.AdminOnly,
        policy => policy.RequireRole(
            UserRole.Admin.ToString()))
    .AddPolicy(
        AuthorizationPolicies.AgentOrAdmin,
        policy => policy.RequireRole(
            UserRole.Admin.ToString(),
            UserRole.Agent.ToString()));

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
});

var app = builder.Build();

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
