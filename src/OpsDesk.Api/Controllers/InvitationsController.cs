using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Invitations.DTOs;
using OpsDesk.Application.Invitations.Interfaces;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("auth/invitations")]
public sealed class InvitationsController(IInvitationService invitations) : ControllerBase
{
    // Allows a recipient without an account to accept the invitation using its secret token.
    [AllowAnonymous]
    [HttpPost("accept")]
    [ProducesResponseType<AcceptedInvitationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AcceptedInvitationResponse>> Accept(
        AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        AcceptedInvitationResponse response = await invitations.AcceptAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
