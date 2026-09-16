using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpsDesk.Application.Audit.Interfaces;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Sla.Interfaces;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Infrastructure.Authentication;
using OpsDesk.Infrastructure.Email;
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
        services.AddOptions<EmailSettings>()
            .Bind(
                configuration.GetSection(
                    EmailSettings.SectionName))
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<EmailSettings>,
            EmailSettingsValidator>();

        services.AddDbContext<OpsDeskDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IExternalIdentityRepository, ExternalIdentityRepository>();
        services.AddScoped<IPasswordRecoveryQueue, PasswordRecoveryQueue>();
        services.AddScoped<IEmailVerificationQueue, EmailVerificationQueue>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
        services.AddSingleton<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddSingleton<IInvitationTokenGenerator, InvitationTokenGenerator>();
        services.AddSingleton<IInvitationEmailSender, SmtpInvitationEmailSender>();
        services.AddScoped<
            IEmailVerificationTokenRepository,
            EmailVerificationTokenRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ISlaPolicyRepository, SlaPolicyRepository>();
        services.AddScoped<ISlaBreachRepository, SlaBreachRepository>();
        services.AddScoped<AdminUserSeeder>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<
            IEmailVerificationTokenGenerator,
            EmailVerificationTokenGenerator>();
        services.AddSingleton<
            IEmailVerificationEmailSender,
            SmtpEmailVerificationEmailSender>();

        return services;
    }
}
