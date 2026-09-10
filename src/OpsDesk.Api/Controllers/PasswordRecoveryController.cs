using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpsDesk.Api.RateLimiting;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class PasswordRecoveryController(IPasswordRecoveryService recovery) : ControllerBase
{
    // Uses the reset credential instead of a JWT; successful reset does not automatically sign the user in.
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [EnableRateLimiting(PasswordRecoveryRateLimiting.ResetPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await recovery.ResetAsync(request, cancellationToken);
        return NoContent();
    }

    // Acknowledges the durable request without exposing account eligibility or delivery state.
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting(PasswordRecoveryRateLimiting.RequestPolicy)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await recovery.RequestAsync(request.Email, cancellationToken);
        return Accepted(new { message = "If the account is eligible, password recovery instructions will be sent." });
    }
}
