using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("auth/email-verification")]
public sealed class EmailVerificationController(
    IEmailVerificationService emailVerificationService) : ControllerBase
{
    // Returns the same result for known and unknown addresses to protect account privacy.
    [AllowAnonymous]
    [HttpPost("resend")]
    public async Task<IActionResult> Resend(
        ResendVerificationEmailRequest request,
        CancellationToken cancellationToken)
    {
        await emailVerificationService.ResendAsync(
            request.Email,
            cancellationToken);

        return NoContent();
    }

    // Verifies the account and consumes the token through the Application service.
    [AllowAnonymous]
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(
        ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        await emailVerificationService.ConfirmAsync(
            request.Token,
            cancellationToken);

        return NoContent();
    }
}
