using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OpsDesk.Api.Configuration;

namespace OpsDesk.Tests.Configuration;

public sealed class SsoSettingsValidatorTests
{
    // Disabled SSO intentionally permits an empty provider configuration.
    [Fact]
    public void Disabled_settings_should_be_valid()
    {
        var validator = new SsoSettingsValidator(new TestHostEnvironment(Environments.Production));

        Microsoft.Extensions.Options.ValidateOptionsResult result =
            validator.Validate(null, new SsoSettings());

        Assert.True(result.Succeeded);
    }

    // Local loopback HTTP is permitted only for the development bootstrap.
    [Fact]
    public void Enabled_local_development_settings_should_be_valid()
    {
        var validator = new SsoSettingsValidator(new TestHostEnvironment(Environments.Development));

        Microsoft.Extensions.Options.ValidateOptionsResult result = validator.Validate(null, ValidSettings());

        Assert.True(result.Succeeded);
    }

    // Production endpoints must use HTTPS even if every other field is complete.
    [Fact]
    public void Production_http_settings_should_be_rejected()
    {
        var validator = new SsoSettingsValidator(new TestHostEnvironment(Environments.Production));

        Microsoft.Extensions.Options.ValidateOptionsResult result = validator.Validate(null, ValidSettings());

        Assert.True(result.Failed);
        Assert.Contains("HTTP only on loopback", result.FailureMessage, StringComparison.Ordinal);
    }

    // Wildcards, paths and partial client credentials are rejected before the first request.
    [Fact]
    public void Partial_or_broad_settings_should_be_rejected()
    {
        var validator = new SsoSettingsValidator(new TestHostEnvironment(Environments.Development));
        SsoSettings settings = ValidSettings();
        settings.ClientSecret = string.Empty;
        settings.PublicOrigin = "http://localhost:5044/untrusted-path";
        settings.AllowedEmailDomains = ["*.example.com"];

        Microsoft.Extensions.Options.ValidateOptionsResult result = validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("ClientSecret", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("only scheme, host", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("exact domain", result.FailureMessage, StringComparison.Ordinal);
    }

    private static SsoSettings ValidSettings()
    {
        return new SsoSettings
        {
            Enabled = true,
            Authority = "http://localhost:8180/realms/opsdesk",
            ClientId = "opsdesk-api",
            ClientSecret = "local-test-secret",
            PublicOrigin = "http://localhost:5044",
            AllowedEmailDomains = ["example.com"]
        };
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "OpsDesk.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
