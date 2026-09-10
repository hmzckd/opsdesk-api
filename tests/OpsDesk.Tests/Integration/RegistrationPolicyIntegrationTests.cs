using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpsDesk.Application.Agents.DTOs;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Common.Exceptions;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class RegistrationPolicyIntegrationTests(OpsDeskApiFixture fixture)
{
    // A malformed boolean must fail binding at startup instead of being silently accepted as a policy.
    [Fact]
    public void Invalid_registration_setting_should_fail_startup()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Registration:PublicRegistrationEnabled"] = "not-a-boolean" })));
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
        {
            using HttpClient client = factory.CreateClient();
        });
        Assert.Contains("Registration:PublicRegistrationEnabled", error.Message);
    }

    // Closing signup does not strand accounts waiting to use an already delivered verification token.
    [Fact]
    public async Task Disabled_registration_should_preserve_existing_email_confirmation()
    {
        string email = $"pending-verification-{Guid.NewGuid():N}@example.com";
        using HttpClient setup = fixture.Factory.CreateClient();
        using HttpResponseMessage registration = await setup.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Pending", "Customer", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        AuthResponse account = Assert.IsType<AuthResponse>(await registration.Content.ReadFromJsonAsync<AuthResponse>());
        var verification = Assert.Single(fixture.Factory.SentEmails, message => message.RecipientEmail == email);
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Registration:PublicRegistrationEnabled"] = "false" })));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage confirmed = await client.PostAsJsonAsync("/auth/email-verification/confirm",
            new ConfirmEmailRequest(verification.RawToken));
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
    }

    // Environment names do not implicitly grant registration; only explicit non-production enablement does.
    [Theory]
    [InlineData("Development", "true", HttpStatusCode.Created)]
    [InlineData("Demo", "true", HttpStatusCode.Created)]
    [InlineData("Development", "false", HttpStatusCode.Forbidden)]
    [InlineData("Production", "false", HttpStatusCode.Forbidden)]
    [InlineData("Production", null, HttpStatusCode.Forbidden)]
    [InlineData("Staging", null, HttpStatusCode.Forbidden)]
    public async Task Environment_configuration_should_control_public_registration(
        string environment, string? enabled, HttpStatusCode expected)
    {
        // Initialize the disposable schema through the normal Testing host; Production never migrates it.
        using HttpClient setup = fixture.Factory.CreateClient();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Registration:PublicRegistrationEnabled"] = enabled,
                    ["AdminSeed:Enabled"] = "false",
                    ["Email:UseSsl"] = "true",
                    ["Email:VerificationUrl"] =
                        "https://opsdesk.example.com/auth/email-verification/confirm"
                }));
        });
        using HttpClient client = factory.CreateClient();
        Assert.Equal(environment, factory.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);
        string email = $"environment-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Environment", "Visitor", email, "ValidPass!"));
        Assert.Equal(expected, response.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(expected == HttpStatusCode.Created ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, login.StatusCode);
        if (expected == HttpStatusCode.Created)
        {
            Assert.Single(fixture.Factory.SentEmails, message => message.RecipientEmail == email);
        }
        else
        {
            Assert.DoesNotContain(fixture.Factory.SentEmails, message => message.RecipientEmail == email);
        }
    }

    // Guards the public Application interface as well as HTTP, without mocking its dependencies.
    [Fact]
    public async Task Disabled_registration_should_also_reject_direct_service_calls()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Registration:PublicRegistrationEnabled"] = "false" })));
        using HttpClient client = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        IAuthService auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        string email = $"direct-disabled-{Guid.NewGuid():N}@example.com";
        await Assert.ThrowsAsync<ForbiddenException>(() => auth.RegisterAsync(
            new RegisterRequest("Blocked", "Visitor", email, "ValidPass!")));
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    // Invitation onboarding and password recovery remain available on a closed Production registration host.
    [Fact]
    public async Task Disabled_registration_should_preserve_invitation_login_and_password_recovery()
    {
        using HttpClient setup = fixture.Factory.CreateClient();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Registration:PublicRegistrationEnabled"] = "false",
                    ["AdminSeed:Enabled"] = "false",
                    ["PasswordRecovery:WorkerEnabled"] = "true",
                    ["Email:UseSsl"] = "true",
                    ["Email:VerificationUrl"] =
                        "https://opsdesk.example.com/auth/email-verification/confirm"
                }));
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage adminLogin = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
        AuthResponse admin = Assert.IsType<AuthResponse>(await adminLogin.Content.ReadFromJsonAsync<AuthResponse>());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        string email = $"closed-invite-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage invite = await client.PostAsJsonAsync("/admin/invitations", new { email, role = "customer" });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var delivered = Assert.Single(fixture.Factory.SentInvitations, message => message.RecipientEmail == email);
        client.DefaultRequestHeaders.Authorization = null;
        using HttpResponseMessage accepted = await client.PostAsJsonAsync("/auth/invitations/accept",
            new { token = delivered.RawToken, firstName = "Invited", lastName = "Customer", password = "ValidPass!" });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var recovery = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        using HttpResponseMessage reset = await client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordRequest(recovery.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using HttpResponseMessage newLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        AuthResponse recovered = Assert.IsType<AuthResponse>(await newLogin.Content.ReadFromJsonAsync<AuthResponse>());
        Assert.Equal("Customer", recovered.Role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recovered.AccessToken);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
    }

    // Closing self-registration does not remove the administrator's existing Agent provisioning capability.
    [Fact]
    public async Task Disabled_registration_should_preserve_admin_agent_provisioning()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Registration:PublicRegistrationEnabled"] = "false" })));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse admin = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        string email = $"closed-agent-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage created = await client.PostAsJsonAsync("/admin/agents",
            new CreateAgentRequest("Created", "Agent", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using HttpResponseMessage agentLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.OK, agentLogin.StatusCode);
        AuthResponse agent = Assert.IsType<AuthResponse>(await agentLogin.Content.ReadFromJsonAsync<AuthResponse>());
        Assert.Equal("Agent", agent.Role);
    }

    // Production must refuse an explicit enablement instead of starting with public signup exposed.
    [Fact]
    public void Production_with_enabled_registration_should_fail_startup()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Registration:PublicRegistrationEnabled"] = "true",
                    ["AdminSeed:Enabled"] = "false",
                    ["Email:UseSsl"] = "true",
                    ["Email:VerificationUrl"] =
                        "https://opsdesk.example.com/auth/email-verification/confirm"
                }));
        });
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() =>
        {
            using HttpClient client = factory.CreateClient();
        });
        Assert.Contains("Production", error.Message);
        Assert.Contains("Registration:PublicRegistrationEnabled", error.Message);
    }

    // A forbidden registration must not leave an account that can subsequently log in.
    [Fact]
    public async Task Disabled_registration_should_reject_the_request_without_creating_an_account()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Registration:PublicRegistrationEnabled"] = "false" })));
        using HttpClient client = factory.CreateClient();
        string email = $"registration-disabled-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Blocked", "Visitor", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.DoesNotContain(fixture.Factory.SentEmails, message => message.RecipientEmail == email);
    }
}
