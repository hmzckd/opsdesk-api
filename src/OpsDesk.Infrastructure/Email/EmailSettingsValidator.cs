using System.Net.Mail;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace OpsDesk.Infrastructure.Email;

public sealed class EmailSettingsValidator(
    IHostEnvironment environment) :
    IValidateOptions<EmailSettings>
{
    /// <summary>
    /// Validates email delivery settings before the API begins serving requests.
    /// </summary>
    public ValidateOptionsResult Validate(
        string? name,
        EmailSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            return ValidateOptionsResult.Fail(
                "Email:Host is required.");
        }

        if (settings.Port is < 1 or > 65535)
        {
            return ValidateOptionsResult.Fail(
                "Email:Port must be between 1 and 65535.");
        }

        if (!MailAddress.TryCreate(
                settings.FromAddress,
                out _))
        {
            return ValidateOptionsResult.Fail(
                "Email:FromAddress must be a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(settings.FromName))
        {
            return ValidateOptionsResult.Fail(
                "Email:FromName is required.");
        }

        bool hasAbsoluteVerificationUrl =
            Uri.TryCreate(
                settings.VerificationUrl,
                UriKind.Absolute,
                out Uri? verificationUri);

        bool hasSupportedScheme =
            hasAbsoluteVerificationUrl &&
            (verificationUri!.Scheme == Uri.UriSchemeHttp ||
             verificationUri.Scheme == Uri.UriSchemeHttps);

        if (!hasSupportedScheme)
        {
            return ValidateOptionsResult.Fail(
                "Email:VerificationUrl must be an absolute HTTP or HTTPS URL.");
        }

        bool permitsLocalInsecureTransport =
            environment.IsDevelopment() ||
            environment.IsEnvironment("Testing");
        if (!permitsLocalInsecureTransport)
        {
            List<string> failures = [];
            if (!settings.UseSsl)
            {
                failures.Add(
                    "Email:UseSsl must be true outside Development and Testing.");
            }

            if (verificationUri!.Scheme != Uri.UriSchemeHttps)
            {
                failures.Add(
                    "Email:VerificationUrl must use HTTPS outside Development and Testing.");
            }

            if (failures.Count > 0)
            {
                return ValidateOptionsResult.Fail(failures);
            }
        }

        return ValidateOptionsResult.Success;
    }
}
