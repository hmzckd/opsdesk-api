using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpsDesk.Api.Configuration;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Api.Authentication;

public sealed class SsoOpenIdConnectEvents(
    IExternalSignInService externalSignIn,
    IOptions<SsoSettings> ssoOptions,
    ILogger<SsoOpenIdConnectEvents> logger) : OpenIdConnectEvents
{
    // Uses the configured public origin instead of trusting a request Host header for the callback URI.
    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        context.ProtocolMessage.RedirectUri = BuildPublicUri("/signin-oidc");
        return Task.CompletedTask;
    }

    // Saves only the already validated token issuer in protected authentication state.
    public override Task TokenValidated(TokenValidatedContext context)
    {
        AuthenticationProperties properties = context.Properties ??
            throw new UnauthorizedAccessException("The external identity is missing protected state.");
        properties.Items[SsoAuthenticationSchemes.IssuerItem] = context.SecurityToken.Issuer;
        return Task.CompletedTask;
    }

    // Converts validated provider claims into an OpsDesk account and writes the local JWT in the response body.
    public override async Task TicketReceived(TicketReceivedContext context)
    {
        ClaimsPrincipal principal = context.Principal ??
            throw new UnauthorizedAccessException("The external identity could not be validated.");
        AuthenticationProperties properties = context.Properties ??
            throw new UnauthorizedAccessException("The external identity is missing protected state.");
        string issuer = GetRequiredItem(properties, SsoAuthenticationSchemes.IssuerItem);
        properties.Items.TryGetValue(SsoAuthenticationSchemes.InvitationHashItem, out string? invitationHash);
        var identity = new VerifiedExternalIdentity(
            issuer,
            GetRequiredClaim(principal, "sub"),
            GetRequiredClaim(principal, "email"),
            bool.TryParse(principal.FindFirstValue("email_verified"), out bool emailVerified) && emailVerified,
            GetRequiredClaim(principal, "given_name"),
            GetRequiredClaim(principal, "family_name"));

        AuthResponse response = await externalSignIn.SignInAsync(
            identity, invitationHash, context.HttpContext.RequestAborted);
        context.HandleResponse();
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        await context.Response.WriteAsJsonAsync(response, context.HttpContext.RequestAborted);
    }

    // Returns a generic protocol error without exposing provider tokens, claims or exception messages.
    public override async Task RemoteFailure(RemoteFailureContext context)
    {
        logger.LogWarning("External sign-in failed with {FailureType}.",
            context.Failure?.GetType().Name ?? "UnknownFailure");
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "External sign-in failed",
            Detail = "The identity provider response could not be accepted.",
            Instance = context.Request.Path
        }, context.HttpContext.RequestAborted);
    }

    private string BuildPublicUri(string path)
    {
        return $"{ssoOptions.Value.PublicOrigin.TrimEnd('/')}{path}";
    }

    private static string GetRequiredClaim(ClaimsPrincipal principal, string claimType)
    {
        return principal.FindFirstValue(claimType) ??
            throw new UnauthorizedAccessException("The external identity is missing required claims.");
    }

    private static string GetRequiredItem(AuthenticationProperties properties, string key)
    {
        if (properties.Items.TryGetValue(key, out string? value) && value is not null)
        {
            return value;
        }

        throw new UnauthorizedAccessException("The external identity is missing validated state.");
    }
}
