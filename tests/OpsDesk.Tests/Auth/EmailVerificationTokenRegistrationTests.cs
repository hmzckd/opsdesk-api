using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Infrastructure;
using OpsDesk.Infrastructure.Authentication;

namespace OpsDesk.Tests.Auth;

public sealed class EmailVerificationTokenRegistrationTests
{
    [Fact]
    public void Add_infrastructure_should_register_token_generator_as_singleton()
    {
        var configurationValues =
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=opsdesk_registration_tests;" +
                    "Username=postgres;Password=postgres"
            };

        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(configurationValues)
                .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using ServiceProvider provider =
            services.BuildServiceProvider();

        IEmailVerificationTokenGenerator first =
            provider.GetRequiredService<
                IEmailVerificationTokenGenerator>();
        IEmailVerificationTokenGenerator second =
            provider.GetRequiredService<
                IEmailVerificationTokenGenerator>();

        Assert.IsType<EmailVerificationTokenGenerator>(first);
        Assert.Same(first, second);
    }
}
