using Microsoft.Extensions.Options;

namespace OpsDesk.Api.Configuration;

public sealed class SsoSettingsValidator(IHostEnvironment environment) : IValidateOptions<SsoSettings>
{
    // Rejects partial or unsafe SSO configuration during application startup.
    public ValidateOptionsResult Validate(string? name, SsoSettings settings)
    {
        if (!settings.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        Uri? authority = ValidateAbsoluteUri(settings.Authority, "Sso:Authority", failures);
        Uri? publicOrigin = ValidateAbsoluteUri(settings.PublicOrigin, "Sso:PublicOrigin", failures);

        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            failures.Add("Sso:ClientId is required when SSO is enabled.");
        }

        if (string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            failures.Add("Sso:ClientSecret is required when SSO is enabled.");
        }

        if (settings.AllowedEmailDomains.Length == 0 ||
            settings.AllowedEmailDomains.Any(domain => !IsValidDomain(domain)))
        {
            failures.Add("Sso:AllowedEmailDomains must contain valid exact domain names.");
        }

        if (publicOrigin is not null &&
            (publicOrigin.AbsolutePath != "/" || !string.IsNullOrEmpty(publicOrigin.Query) ||
             !string.IsNullOrEmpty(publicOrigin.Fragment)))
        {
            failures.Add("Sso:PublicOrigin must contain only scheme, host and optional port.");
        }

        ValidateTransport(authority, "Sso:Authority", failures);
        ValidateTransport(publicOrigin, "Sso:PublicOrigin", failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static Uri? ValidateAbsoluteUri(string value, string key, List<string> failures)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            return uri;
        }

        failures.Add($"{key} must be an absolute HTTP or HTTPS URI.");
        return null;
    }

    private void ValidateTransport(Uri? uri, string key, List<string> failures)
    {
        if (uri is null || uri.Scheme == Uri.UriSchemeHttps)
        {
            return;
        }

        bool localEnvironment = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        if (!localEnvironment || !uri.IsLoopback)
        {
            failures.Add($"{key} may use HTTP only on loopback in Development or Testing.");
        }
    }

    private static bool IsValidDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain) || domain.Length > 253 ||
            domain.StartsWith(".", StringComparison.Ordinal) || domain.EndsWith(".", StringComparison.Ordinal))
        {
            return false;
        }

        return domain.Split('.').All(label => label.Length is > 0 and <= 63 &&
            label[0] != '-' && label[^1] != '-' &&
            label.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'));
    }
}
