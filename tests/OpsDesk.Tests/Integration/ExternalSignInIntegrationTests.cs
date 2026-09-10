using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Invitations.Interfaces;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class ExternalSignInIntegrationTests(OpsDeskApiFixture fixture)
{
    private const string Issuer = "https://identity.opsdesk.test/realms/opsdesk";

    // Enabling valid SSO settings exposes a browser form without contacting the provider yet.
    [Fact]
    public async Task Enabled_sso_should_offer_a_browser_start_page()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Sso:Enabled"] = "true",
                    ["Sso:Authority"] = "http://localhost:8180/realms/opsdesk",
                    ["Sso:ClientId"] = "opsdesk-api",
                    ["Sso:ClientSecret"] = "test-client-secret",
                    ["Sso:PublicOrigin"] = "http://localhost:5044",
                    ["Sso:AllowedEmailDomains:0"] = "example.com"
                })));
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using HttpResponseMessage response = await client.GetAsync("/auth/sso/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        string html = await response.Content.ReadAsStringAsync();
        Assert.Contains("action=\"/auth/sso/login\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"invitationCode\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("test-client-secret", html, StringComparison.Ordinal);
    }

    // The form starts code+PKCE while keeping the raw invitation out of every redirect URL.
    [Fact]
    public async Task Valid_browser_form_should_challenge_keycloak_without_exposing_the_invitation()
    {
        using var factory = CreateTransportFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        using HttpResponseMessage page = await client.GetAsync("/auth/sso/login");
        string html = await page.Content.ReadAsStringAsync();
        Match tokenMatch = Regex.Match(html,
            "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success);
        string antiForgeryToken = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
        string invitationCode;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            invitationCode = scope.ServiceProvider.GetRequiredService<IInvitationTokenGenerator>()
                .GenerateToken().RawToken;
        }
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiForgeryToken,
            ["invitationCode"] = invitationCode
        });

        using HttpResponseMessage response = await client.PostAsync("/auth/sso/login", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Uri location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("identity.opsdesk.test", location.Host);
        Assert.Contains("response_type=code", location.Query, StringComparison.Ordinal);
        Assert.Contains("code_challenge=", location.Query, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", location.Query, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("http://localhost:5044/signin-oidc"), location.Query,
            StringComparison.Ordinal);
        Assert.DoesNotContain(invitationCode, location.OriginalString, StringComparison.Ordinal);
    }

    // A signed provider callback creates the invited account and returns an immediately usable OpsDesk JWT.
    [Fact]
    public async Task Valid_provider_callback_should_create_the_invited_account()
    {
        using RSA rsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(rsa) { KeyId = "opsdesk-sso-test-key" };
        var provider = new FakeOidcBackchannel(signingKey, "opsdesk-api");
        using var factory = CreateCallbackFactory(provider, signingKey);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        string email = $"oidc-{Guid.NewGuid():N}@example.com";
        using (HttpResponseMessage adminLogin = await client.PostAsJsonAsync("/auth/login",
                   new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword)))
        {
            AuthResponse admin = Assert.IsType<AuthResponse>(
                await adminLogin.Content.ReadFromJsonAsync<AuthResponse>());
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        }
        using (HttpResponseMessage invitation = await client.PostAsJsonAsync(
                   "/admin/invitations", new { email, role = "agent" }))
        {
            Assert.Equal(HttpStatusCode.Created, invitation.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = null;
        string invitationCode = Assert.Single(fixture.Factory.SentInvitations,
            message => message.RecipientEmail == email).RawToken;
        using HttpResponseMessage page = await client.GetAsync("/auth/sso/login");
        string html = await page.Content.ReadAsStringAsync();
        Match tokenMatch = Regex.Match(html,
            "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success);
        string antiForgeryToken = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiForgeryToken,
            ["invitationCode"] = invitationCode
        });
        using HttpResponseMessage challenge = await client.PostAsync("/auth/sso/login", form);
        Uri authorizationUri = Assert.IsType<Uri>(challenge.Headers.Location);
        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query =
            QueryHelpers.ParseQuery(authorizationUri.Query);
        provider.Nonce = query["nonce"].ToString();
        provider.Subject = Guid.NewGuid().ToString();
        provider.Email = email;
        string callback = QueryHelpers.AddQueryString("/signin-oidc", new Dictionary<string, string?>
        {
            ["code"] = "single-use-test-code",
            ["state"] = query["state"].ToString()
        });

        using HttpResponseMessage response = await client.GetAsync(callback);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AuthResponse created = Assert.IsType<AuthResponse>(await response.Content.ReadFromJsonAsync<AuthResponse>());
        Assert.Equal(email, created.Email);
        Assert.Equal("Agent", created.Role);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.DoesNotContain(response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies)
            ? cookies : [], cookie => cookie.StartsWith("OpsDesk.Sso.Temporary=", StringComparison.Ordinal));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", created.AccessToken);
        using HttpResponseMessage me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    // Disabled SSO is not advertised and cannot accidentally start a provider flow.
    [Fact]
    public async Task Disabled_sso_should_hide_the_browser_endpoint()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/auth/sso/login");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // A cross-site or stale form cannot start an OIDC challenge without the paired anti-forgery token.
    [Fact]
    public async Task Browser_form_without_antiforgery_token_should_be_rejected()
    {
        using var factory = CreateTransportFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["invitationCode"] = string.Empty
        });

        using HttpResponseMessage response = await client.PostAsync("/auth/sso/login", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Callback state must have been generated by this OpsDesk instance and paired with its browser cookie.
    [Fact]
    public async Task Provider_callback_with_unknown_state_should_be_rejected()
    {
        using var factory = CreateTransportFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        using HttpResponseMessage response = await client.GetAsync(
            "/signin-oidc?code=untrusted-code&state=untrusted-state");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Local JWT revocation is immediate; provider browser logout remains an explicit second step.
    [Fact]
    public async Task Logout_should_revoke_the_local_token_and_return_a_provider_url()
    {
        using var factory = CreateTransportFactory();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        AuthResponse account = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

        using HttpResponseMessage response = await client.PostAsync("/auth/sso/logout", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using System.Text.Json.JsonDocument body =
            System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("localSessionsRevoked").GetBoolean());
        string providerLogoutUrl = Assert.IsType<string>(
            body.RootElement.GetProperty("providerLogoutUrl").GetString());
        Assert.StartsWith($"{Issuer}/protocol/openid-connect/logout?", providerLogoutUrl,
            StringComparison.Ordinal);
        Assert.Contains("client_id=opsdesk-api", providerLogoutUrl, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("http://localhost:5044/auth/sso/signed-out"),
            providerLogoutUrl, StringComparison.Ordinal);
        Assert.DoesNotContain(account.AccessToken, providerLogoutUrl, StringComparison.Ordinal);
        using HttpResponseMessage me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    private WebApplicationFactory<Program> CreateTransportFactory()
    {
        return fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Sso:Enabled"] = "true",
                    ["Sso:Authority"] = "https://identity.opsdesk.test/realms/opsdesk",
                    ["Sso:ClientId"] = "opsdesk-api",
                    ["Sso:ClientSecret"] = "test-client-secret",
                    ["Sso:PublicOrigin"] = "http://localhost:5044",
                    ["Sso:AllowedEmailDomains:0"] = "example.com"
                }));
            builder.ConfigureServices(services => services.PostConfigure<OpenIdConnectOptions>(
                "OpsDesk.Sso.OpenIdConnect",
                options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint =
                            "https://identity.opsdesk.test/realms/opsdesk/protocol/openid-connect/auth",
                        EndSessionEndpoint =
                            "https://identity.opsdesk.test/realms/opsdesk/protocol/openid-connect/logout"
                    };
                    options.Configuration = configuration;
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                }));
        });
    }

    private WebApplicationFactory<Program> CreateCallbackFactory(
        FakeOidcBackchannel provider, SecurityKey signingKey)
    {
        return fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Sso:Enabled"] = "true",
                    ["Sso:Authority"] = Issuer,
                    ["Sso:ClientId"] = "opsdesk-api",
                    ["Sso:ClientSecret"] = "test-client-secret",
                    ["Sso:PublicOrigin"] = "http://localhost:5044",
                    ["Sso:AllowedEmailDomains:0"] = "example.com"
                }));
            builder.ConfigureServices(services => services.PostConfigure<OpenIdConnectOptions>(
                "OpsDesk.Sso.OpenIdConnect",
                options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = Issuer,
                        AuthorizationEndpoint = $"{Issuer}/protocol/openid-connect/auth",
                        TokenEndpoint = $"{Issuer}/protocol/openid-connect/token"
                    };
                    configuration.SigningKeys.Add(signingKey);
                    options.Configuration = configuration;
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                    options.Backchannel = new HttpClient(provider);
                }));
        });
    }

    private sealed class FakeOidcBackchannel(SecurityKey signingKey, string audience) : HttpMessageHandler
    {
        public string Nonce { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        // Represents only the external token endpoint; all OpsDesk components remain real in the test.
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal($"{Issuer}/protocol/openid-connect/token", request.RequestUri?.AbsoluteUri);
            DateTime now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                Issuer,
                audience,
                [
                    new Claim("sub", Subject),
                    new Claim("email", Email),
                    new Claim("email_verified", "true", ClaimValueTypes.Boolean),
                    new Claim("given_name", "OIDC"),
                    new Claim("family_name", "Agent"),
                    new Claim("nonce", Nonce),
                    new Claim(JwtRegisteredClaimNames.Iat,
                        EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64)
                ],
                now.AddMinutes(-1),
                now.AddMinutes(5),
                new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256));
            string idToken = new JwtSecurityTokenHandler().WriteToken(token);
            string json = System.Text.Json.JsonSerializer.Serialize(new
            {
                access_token = "provider-access-token",
                token_type = "Bearer",
                expires_in = 300,
                id_token = idToken
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    // A rejected identity must not consume the invitation that its real owner can still accept.
    [Theory]
    [InlineData("issuer")]
    [InlineData("unverified")]
    [InlineData("domain")]
    [InlineData("email")]
    [InlineData("subject")]
    [InlineData("invitation")]
    public async Task Unapproved_identity_should_be_rejected_without_consuming_invitation(string invalidField)
    {
        using var factory = CreateEnabledFactory();
        using HttpClient client = factory.CreateClient();
        string email = $"sso-denied-{Guid.NewGuid():N}@example.com";
        string hash = await InviteAsync(factory, client, email);
        var valid = new VerifiedExternalIdentity(Issuer, Guid.NewGuid().ToString(), email, true, "SSO", "User");
        VerifiedExternalIdentity invalid = invalidField switch
        {
            "issuer" => valid with { Issuer = "https://unapproved.opsdesk.test/realms/opsdesk" },
            "unverified" => valid with { EmailVerified = false },
            "domain" => valid with { Email = "person@unapproved.example" },
            "email" => valid with { Email = $"other-{Guid.NewGuid():N}@example.com" },
            "subject" => valid with { Subject = " " },
            _ => valid
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => SignInAsync(factory, invalid,
            invalidField == "invitation" ? null : hash));
        AuthResponse accepted = await SignInAsync(factory, valid, hash);
        Assert.Equal(email, accepted.Email);
        Assert.Equal("Customer", accepted.Role);
    }

    // Competing identities cannot both consume the same invitation, even in separate requests.
    [Fact]
    public async Task Concurrent_external_acceptance_should_have_one_winner()
    {
        using var factory = CreateEnabledFactory();
        using HttpClient client = factory.CreateClient();
        string email = $"sso-race-{Guid.NewGuid():N}@example.com";
        string hash = await InviteAsync(factory, client, email);
        var first = new VerifiedExternalIdentity(Issuer, Guid.NewGuid().ToString(), email, true, "First", "User");
        var second = first with { Subject = Guid.NewGuid().ToString(), FirstName = "Second" };
        Task<AuthResponse>[] attempts = [SignInAsync(factory, first, hash), SignInAsync(factory, second, hash)];
        Exception? error = await Record.ExceptionAsync(() => Task.WhenAll(attempts));

        Assert.IsType<ForbiddenException>(error);
        Task<AuthResponse> winner = Assert.Single(attempts, attempt => attempt.IsCompletedSuccessfully);
        _ = Assert.Single(attempts, attempt => attempt.IsFaulted);
        VerifiedExternalIdentity winningIdentity = winner == attempts[0] ? first : second;
        VerifiedExternalIdentity losingIdentity = winner == attempts[0] ? second : first;
        AuthResponse returning = await SignInAsync(factory, winningIdentity, null);
        AuthResponse created = await winner;
        Assert.Equal(created.UserId, returning.UserId);
        await Assert.ThrowsAsync<ForbiddenException>(() => SignInAsync(factory, losingIdentity, null));
    }

    // Matching email alone must not link or overwrite an existing local account, and failures roll back.
    [Fact]
    public async Task Existing_local_account_should_not_be_linked_or_modified()
    {
        using var factory = CreateEnabledFactory();
        using HttpClient client = factory.CreateClient();
        string email = $"sso-existing-{Guid.NewGuid():N}@example.com";
        string hash = await InviteAsync(factory, client, email, "agent");
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Original", "Person", email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        AuthResponse original = Assert.IsType<AuthResponse>(await registration.Content.ReadFromJsonAsync<AuthResponse>());
        var identity = new VerifiedExternalIdentity(Issuer, Guid.NewGuid().ToString(), email, true, "SSO", "Agent");

        await Assert.ThrowsAsync<ConflictException>(() => SignInAsync(factory, identity, hash));
        await Assert.ThrowsAsync<ConflictException>(() => SignInAsync(factory, identity, hash));
        await Assert.ThrowsAsync<ForbiddenException>(() => SignInAsync(factory, identity, null));

        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse unchanged = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        Assert.Equal(original.UserId, unchanged.UserId);
        Assert.Equal("Original", unchanged.FirstName);
        Assert.Equal("Customer", unchanged.Role);
    }

    // Without explicit provider configuration, even a plausible verified identity is rejected.
    [Fact]
    public async Task External_sign_in_should_be_disabled_by_default()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        var identity = new VerifiedExternalIdentity(Issuer, Guid.NewGuid().ToString(),
            "disabled@example.com", true, "SSO", "User");

        await Assert.ThrowsAsync<ForbiddenException>(() => SignInAsync(fixture.Factory, identity, null));
    }

    // Each host gets an explicit trusted test provider; production remains disabled by default.
    private WebApplicationFactory<Program> CreateEnabledFactory()
    {
        return fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ExternalSignInPolicy>();
            services.AddSingleton(new ExternalSignInPolicy(true, Issuer, ["example.com"]));
        }));
    }

    // Creates a real administrator invitation through HTTP and returns only its stored-token hash.
    private async Task<string> InviteAsync(WebApplicationFactory<Program> factory, HttpClient client,
        string email, string role = "customer")
    {
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse admin = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        using HttpResponseMessage invited = await client.PostAsJsonAsync("/admin/invitations", new { email, role });
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        string raw = Assert.Single(fixture.Factory.SentInvitations, message => message.RecipientEmail == email).RawToken;
        using IServiceScope scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IInvitationTokenGenerator>().ComputeHash(raw);
    }

    // Separate scopes model separate requests and prevent accidental shared DbContext state in races.
    private static async Task<AuthResponse> SignInAsync(WebApplicationFactory<Program> factory,
        VerifiedExternalIdentity identity, string? invitationHash)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IExternalSignInService>()
            .SignInAsync(identity, invitationHash);
    }

    // The invitation owns the role; the resulting identity can return without another invitation or local password.
    [Fact]
    public async Task Invited_external_identity_should_create_one_passwordless_account_and_return_to_it()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ExternalSignInPolicy>();
            services.AddSingleton(new ExternalSignInPolicy(true, Issuer, ["example.com"]));
        }));
        using HttpClient client = factory.CreateClient();
        string email = $"sso-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse admin = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        using HttpResponseMessage invited = await client.PostAsJsonAsync("/admin/invitations", new { email, role = "agent" });
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        string raw = Assert.Single(fixture.Factory.SentInvitations, message => message.RecipientEmail == email).RawToken;
        var identity = new VerifiedExternalIdentity(Issuer, Guid.NewGuid().ToString(), email, true, "SSO", "Agent");
        AuthResponse first;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            string hash = scope.ServiceProvider.GetRequiredService<IInvitationTokenGenerator>().ComputeHash(raw);
            first = await scope.ServiceProvider.GetRequiredService<IExternalSignInService>().SignInAsync(identity, hash);
        }
        Assert.Equal("Agent", first.Role);
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AuthResponse again = await scope.ServiceProvider.GetRequiredService<IExternalSignInService>().SignInAsync(identity, null);
            Assert.Equal(first.UserId, again.UserId);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        using HttpResponseMessage me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using HttpResponseMessage passwordLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Unauthorized, passwordLogin.StatusCode);
        using HttpResponseMessage replay = await client.PostAsJsonAsync("/auth/invitations/accept",
            new { token = raw, firstName = "Local", lastName = "User", password = "ValidPass!" });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }
}
