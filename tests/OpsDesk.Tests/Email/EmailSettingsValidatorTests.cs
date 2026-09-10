using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using OpsDesk.Infrastructure.Email;

namespace OpsDesk.Tests.Email;

public sealed class EmailSettingsValidatorTests
{
    [Fact]
    public void Production_insecure_email_transport_should_fail()
    {
        EmailSettings settings = CreateValidSettings();
        var validator = new EmailSettingsValidator(
            new TestHostEnvironment(Environments.Production));

        ValidateOptionsResult result = validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("Email:UseSsl must be true outside Development and Testing.", result.Failures);
        Assert.Contains("Email:VerificationUrl must use HTTPS outside Development and Testing.", result.Failures);
    }

    [Fact]
    public void Validate_with_valid_settings_should_succeed()
    {
        EmailSettings settings = CreateValidSettings();
        EmailSettingsValidator validator = CreateDevelopmentValidator();

        ValidateOptionsResult result =
            validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_without_host_should_fail()
    {
        EmailSettings settings = CreateValidSettings();
        settings.Host = string.Empty;
        EmailSettingsValidator validator = CreateDevelopmentValidator();

        ValidateOptionsResult result =
            validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains(
            "Email:Host is required.",
            result.Failures);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Validate_with_out_of_range_port_should_fail(
        int port)
    {
        EmailSettings settings = CreateValidSettings();
        settings.Port = port;
        EmailSettingsValidator validator = CreateDevelopmentValidator();

        ValidateOptionsResult result =
            validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains(
            "Email:Port must be between 1 and 65535.",
            result.Failures);
    }

    [Fact]
    public void Validate_with_invalid_from_address_should_fail()
    {
        EmailSettings settings = CreateValidSettings();
        settings.FromAddress = "not-an-email-address";
        EmailSettingsValidator validator = CreateDevelopmentValidator();

        ValidateOptionsResult result =
            validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains(
            "Email:FromAddress must be a valid email address.",
            result.Failures);
    }

    [Fact]
    public void Validate_without_from_name_should_fail()
    {
        EmailSettings settings = CreateValidSettings();
        settings.FromName = " ";
        EmailSettingsValidator validator = CreateDevelopmentValidator();

        ValidateOptionsResult result =
            validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains(
            "Email:FromName is required.",
            result.Failures);
    }

    [Theory]
    [InlineData("verify-email")]
    [InlineData("ftp://localhost/verify-email")]
    public void Validate_with_invalid_verification_url_should_fail(
        string verificationUrl)
    {
        EmailSettings settings = CreateValidSettings();
        settings.VerificationUrl = verificationUrl;
        EmailSettingsValidator validator = CreateDevelopmentValidator();

        ValidateOptionsResult result =
            validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains(
            "Email:VerificationUrl must be an absolute HTTP or HTTPS URL.",
            result.Failures);
    }

    private static EmailSettings CreateValidSettings()
    {
        return new EmailSettings
        {
            Host = "localhost",
            Port = 1025,
            UseSsl = false,
            FromAddress = "noreply@opsdesk.local",
            FromName = "OpsDesk",
            VerificationUrl =
                "http://localhost:5044/auth/email-verification/confirm"
        };
    }

    private static EmailSettingsValidator CreateDevelopmentValidator()
    {
        return new EmailSettingsValidator(
            new TestHostEnvironment(Environments.Development));
    }

    private sealed class TestHostEnvironment(string environmentName) :
        IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "OpsDesk.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            null!;
    }
}
