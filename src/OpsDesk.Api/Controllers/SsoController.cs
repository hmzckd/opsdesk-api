using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpsDesk.Api.Configuration;
using OpsDesk.Api.Authentication;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Api.Models;
using System.Security.Claims;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("auth/sso")]
public sealed class SsoController(
    IOptions<SsoSettings> ssoOptions,
    IAntiforgery antiforgery,
    IInvitationTokenGenerator invitationTokens,
    IAuthSessionService sessions,
    SsoProviderLogoutUrlFactory logoutUrls) : ControllerBase
{
    // Renders the small browser bootstrap form and creates its anti-forgery cookie/token pair.
    [AllowAnonymous]
    [HttpGet("login")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Start()
    {
        SsoSettings settings = ssoOptions.Value;
        if (!settings.Enabled)
        {
            return NotFound();
        }

        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(HttpContext);
        string requestToken = WebUtility.HtmlEncode(tokens.RequestToken ??
            throw new InvalidOperationException("The anti-forgery request token was not created."));
        string html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>OpsDesk SSO</title>
            </head>
            <body>
                <main>
                    <h1>OpsDesk</h1>
                    <form method="post" action="/auth/sso/login">
                        <input type="hidden" name="__RequestVerificationToken" value="{{requestToken}}">
                        <label for="invitationCode">Invitation code</label>
                        <input id="invitationCode" name="invitationCode" type="password" autocomplete="off">
                        <button type="submit">Continue with Keycloak</button>
                    </form>
                </main>
            </body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    // Validates the browser form, stores only a token hash in protected state and starts OIDC.
    [AllowAnonymous]
    [HttpPost("login")]
    [Consumes("application/x-www-form-urlencoded")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> BeginSignIn(
        [FromForm] string? invitationCode,
        CancellationToken cancellationToken)
    {
        if (!ssoOptions.Value.Enabled)
        {
            return NotFound();
        }

        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid sign-in form",
                Detail = "Reload the sign-in page and try again."
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        var properties = new AuthenticationProperties
        {
            RedirectUri = "/auth/sso/signed-in"
        };
        if (!string.IsNullOrWhiteSpace(invitationCode))
        {
            properties.Items[SsoAuthenticationSchemes.InvitationHashItem] =
                invitationTokens.ComputeHash(invitationCode);
        }

        return Challenge(properties, SsoAuthenticationSchemes.OpenIdConnect);
    }

    // Revokes local JWTs first and returns the separate browser step for ending the provider session.
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType<SsoLogoutResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SsoLogoutResponse>> Logout(CancellationToken cancellationToken)
    {
        string? userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out Guid userId))
        {
            return Unauthorized();
        }

        await sessions.RevokeAllAsync(userId, cancellationToken);
        string? providerLogoutUrl = await logoutUrls.CreateAsync(cancellationToken);
        return Ok(new SsoLogoutResponse(true, providerLogoutUrl));
    }

    // Gives the identity provider a fixed, harmless destination after browser logout.
    [AllowAnonymous]
    [HttpGet("signed-out")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult SignedOut()
    {
        if (!ssoOptions.Value.Enabled)
        {
            return NotFound();
        }

        const string html = """
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Signed out - OpsDesk</title>
            </head>
            <body>
                <main>
                    <h1>Signed out</h1>
                    <a href="/auth/sso/login">Sign in again</a>
                </main>
            </body>
            </html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }
}
