using Microsoft.Extensions.Options;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Api.Configuration;

public static class RegistrationConfiguration
{
    // Binds host configuration while keeping the Application layer independent of Options packages.
    public static IServiceCollection AddRegistrationConfiguration(this IServiceCollection services)
    {
        services.AddOptions<RegistrationSettings>()
            .BindConfiguration(RegistrationSettings.SectionName)
            .Validate<IHostEnvironment>((settings, environment) =>
                !environment.IsProduction() || !settings.PublicRegistrationEnabled,
                "Registration:PublicRegistrationEnabled must be false in Production.")
            .ValidateOnStart();

        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<RegistrationSettings>>().Value);
        return services;
    }
}
