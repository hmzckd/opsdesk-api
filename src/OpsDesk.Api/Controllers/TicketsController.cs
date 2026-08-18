using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("tickets")]
public sealed class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    /// <summary>
    /// Retrieves one Ticket visible to the authenticated User.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        string? userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        string? userRoleClaim =
            User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(userIdClaim, out Guid viewerId) ||
            !Enum.TryParse(
                userRoleClaim,
                ignoreCase: true,
                out UserRole viewerRole) ||
            !Enum.IsDefined(viewerRole))
        {
            return Unauthorized();
        }

        TicketResponse? response =
            await _ticketService.GetByIdAsync(
                id,
                viewerId,
                viewerRole,
                cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// Creates a Ticket for the authenticated User.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<TicketResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TicketResponse>> Create(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        string? userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out Guid requesterId))
        {
            return Unauthorized();
        }

        TicketResponse response =
            await _ticketService.CreateAsync(
                requesterId,
                request,
                cancellationToken);

        return Created(
            $"/tickets/{response.Id}",
            response);
    }
}
