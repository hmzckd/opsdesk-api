using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Infrastructure.Authentication;
using OpsDesk.Infrastructure.Persistence;
using OpsDesk.Infrastructure.Persistence.Repositories;
using OpsDesk.Infrastructure.Seed;

namespace OpsDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "DefaultConnection connection string was not found.");
        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName));
        services.Configure<AdminSeedSettings>(
            configuration.GetSection(AdminSeedSettings.SectionName));

        services.AddDbContext<OpsDeskDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<AdminUserSeeder>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}