using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OpsDesk.Api.Authentication;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Api.Configuration;

public static class SsoConfiguration
{
    // Binds validated host settings and exposes only the provider policy needed by Application.
    public static IServiceCollection AddSsoConfiguration(this IServiceCollection services)
    {
        services.AddOptions<SsoSettings>()
            .BindConfiguration(SsoSettings.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SsoSettings>, SsoSettingsValidator>();
        services.AddSingleton(provider =>
        {
            SsoSettings settings = provider.GetRequiredService<IOptions<SsoSettings>>().Value;
            return new ExternalSignInPolicy(
                settings.Enabled,
                settings.Authority.TrimEnd('/'),
                settings.AllowedEmailDomains.Select(domain => domain.Trim().ToLowerInvariant()).ToArray());
        });
        services.AddAntiforgery();
        services.AddScoped<SsoOpenIdConnectEvents>();
        services.AddSingleton<SsoProviderLogoutUrlFactory>();
        services.AddAuthentication()
            .AddCookie(SsoAuthenticationSchemes.TemporaryCookie, options =>
            {
                options.Cookie.Name = "OpsDesk.Sso.Temporary";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                options.SlidingExpiration = false;
            })
            .AddOpenIdConnect(SsoAuthenticationSchemes.OpenIdConnect, _ => { });
        services.AddOptions<OpenIdConnectOptions>(SsoAuthenticationSchemes.OpenIdConnect)
            .Configure<IOptions<SsoSettings>, IHostEnvironment>((options, configured, environment) =>
            {
                SsoSettings settings = configured.Value;
                options.Authority = settings.Enabled
                    ? settings.Authority.TrimEnd('/')
                    : "https://sso-disabled.invalid";
                options.ClientId = settings.Enabled ? settings.ClientId : "sso-disabled";
                options.ClientSecret = settings.Enabled ? settings.ClientSecret : "sso-disabled";
                options.SignInScheme = SsoAuthenticationSchemes.TemporaryCookie;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.UsePkce = true;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.MapInboundClaims = false;
                options.CallbackPath = "/signin-oidc";
                options.SignedOutCallbackPath = "/signout-callback-oidc";
                options.EventsType = typeof(SsoOpenIdConnectEvents);
                options.Scope.Clear();
                options.Scope.Add(OpenIdConnectScope.OpenId);
                options.Scope.Add(OpenIdConnectScope.Profile);
                options.Scope.Add(OpenIdConnectScope.Email);
                bool localHttpAuthority = settings.Enabled &&
                    Uri.TryCreate(settings.Authority, UriKind.Absolute, out Uri? authority) &&
                    authority.Scheme == Uri.UriSchemeHttp && authority.IsLoopback &&
                    (environment.IsDevelopment() || environment.IsEnvironment("Testing"));
                bool localHttpOrigin = settings.Enabled &&
                    Uri.TryCreate(settings.PublicOrigin, UriKind.Absolute, out Uri? publicOrigin) &&
                    publicOrigin.Scheme == Uri.UriSchemeHttp && publicOrigin.IsLoopback &&
                    (environment.IsDevelopment() || environment.IsEnvironment("Testing"));
                options.RequireHttpsMetadata = !localHttpAuthority;
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.NonceCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = localHttpOrigin
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.NonceCookie.SecurePolicy = localHttpOrigin
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
            });
        return services;
    }
}
