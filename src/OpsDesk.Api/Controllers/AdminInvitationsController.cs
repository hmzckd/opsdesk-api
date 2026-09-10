using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Authorization;
using OpsDesk.Application.Invitations.DTOs;
using OpsDesk.Application.Invitations.Interfaces;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("admin/invitations")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminInvitationsController(IInvitationService invitations) : ControllerBase
{
    // Takes the inviter identity from the validated JWT, never from user-supplied JSON.
    [HttpPost]
    [ProducesResponseType<InvitationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvitationResponse>> Create(
        CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid invitedById))
        {
            return Unauthorized();
        }

        InvitationResponse response = await invitations.CreateAsync(request, invitedById, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
