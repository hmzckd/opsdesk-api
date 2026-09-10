using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OpsDesk.Api.Configuration;

namespace OpsDesk.Api.Authentication;

public sealed class SsoProviderLogoutUrlFactory(
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    IOptions<SsoSettings> ssoOptions,
    ILogger<SsoProviderLogoutUrlFactory> logger)
{
    // Builds a provider-neutral RP-initiated logout URL from trusted discovery metadata.
    public async Task<string?> CreateAsync(CancellationToken cancellationToken = default)
    {
        SsoSettings settings = ssoOptions.Value;
        if (!settings.Enabled)
        {
            return null;
        }

        try
        {
            OpenIdConnectOptions options = oidcOptions.Get(SsoAuthenticationSchemes.OpenIdConnect);
            OpenIdConnectConfiguration? configuration = options.Configuration;
            if (configuration is null && options.ConfigurationManager is not null)
            {
                configuration = await options.ConfigurationManager.GetConfigurationAsync(cancellationToken);
            }

            if (configuration is null ||
                !Uri.TryCreate(configuration.EndSessionEndpoint, UriKind.Absolute, out Uri? endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
            {
                return null;
            }

            string signedOutUri = $"{settings.PublicOrigin.TrimEnd('/')}/auth/sso/signed-out";
            QueryString query = QueryString.Create([
                new KeyValuePair<string, string?>("client_id", settings.ClientId),
                new KeyValuePair<string, string?>("post_logout_redirect_uri", signedOutUri)
            ]);
            return $"{endpoint}{query}";
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException)
        {
            logger.LogWarning("The SSO provider logout endpoint could not be loaded ({FailureType}).",
                exception.GetType().Name);
            return null;
        }
    }
}
