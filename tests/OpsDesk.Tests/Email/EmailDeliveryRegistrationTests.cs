using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Infrastructure;
using OpsDesk.Infrastructure.Email;

namespace OpsDesk.Tests.Email;

public sealed class EmailDeliveryRegistrationTests
{
    [Fact]
    public void AddInfrastructure_should_register_email_delivery_services()
    {
        IConfiguration configuration =
            CreateConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(
            new TestHostEnvironment());
        services.AddInfrastructure(configuration);

        using ServiceProvider provider =
            services.BuildServiceProvider();

        EmailSettings settings = provider
            .GetRequiredService<IOptions<EmailSettings>>()
            .Value;
        IEmailVerificationEmailSender firstSender =
            provider.GetRequiredService<
                IEmailVerificationEmailSender>();
        IEmailVerificationEmailSender secondSender =
            provider.GetRequiredService<
                IEmailVerificationEmailSender>();

        Assert.Equal("localhost", settings.Host);
        Assert.Equal(1025, settings.Port);
        Assert.IsType<
            SmtpEmailVerificationEmailSender>(firstSender);
        Assert.Same(firstSender, secondSender);
    }

    [Fact]
    public void AddInfrastructure_should_reject_invalid_email_settings()
    {
        IConfiguration configuration =
            CreateConfiguration(host: string.Empty);
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(
            new TestHostEnvironment());
        services.AddInfrastructure(configuration);

        using ServiceProvider provider =
            services.BuildServiceProvider();

        OptionsValidationException exception =
            Assert.Throws<OptionsValidationException>(() =>
                provider
                    .GetRequiredService<
                        IOptions<EmailSettings>>()
                    .Value);

        Assert.Contains(
            "Email:Host is required.",
            exception.Failures);
    }

    private static IConfiguration CreateConfiguration(
        string host = "localhost")
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=localhost;Database=opsdesk_email_tests;" +
                "Username=postgres;Password=postgres",
            ["Email:Host"] = host,
            ["Email:Port"] = "1025",
            ["Email:UseSsl"] = "false",
            ["Email:FromAddress"] =
                "noreply@opsdesk.local",
            ["Email:FromName"] = "OpsDesk",
            ["Email:VerificationUrl"] =
                "http://localhost:5044/" +
                "auth/email-verification/confirm"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } =
            Environments.Development;

        public string ApplicationName { get; set; } =
            "OpsDesk.Tests";

        public string ContentRootPath { get; set; } =
            AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider
            ContentRootFileProvider { get; set; } = null!;
    }
}
