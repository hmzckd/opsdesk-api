using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Authorization;
using OpsDesk.Application.Common.Pagination;
using OpsDesk.Application.Tickets.DTOs;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.VerifiedEmail)]
[Route("tickets")]
public sealed class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    /// <summary>
    /// Lists one page of Tickets visible to the authenticated User.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<TicketListItemResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<
        ActionResult<PagedResponse<TicketListItemResponse>>> List(
            [FromQuery] ListTicketsRequest request,
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

        PagedResponse<TicketListItemResponse> response =
            await _ticketService.ListAsync(
                viewerId,
                viewerRole,
                request,
                cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Retrieves one visible Ticket's role-appropriate activity timeline.
    /// </summary>
    [HttpGet("{id:guid}/activity")]
    [ProducesResponseType<IReadOnlyList<TicketActivityResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<TicketActivityResponse>>>
        GetActivity(
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

        IReadOnlyList<TicketActivityResponse>? response =
            await _ticketService.GetActivityAsync(
                id,
                viewerId,
                viewerRole,
                cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// Assigns a Ticket to an Agent within the actor's ownership scope.
    /// </summary>
    [HttpPut("{id:guid}/assignee")]
    [Authorize(Policy = AuthorizationPolicies.AgentOrAdmin)]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketResponse>> Assign(
        Guid id,
        AssignTicketRequest request,
        CancellationToken cancellationToken)
    {
        string? userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        string? userRoleClaim =
            User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(userIdClaim, out Guid actorId) ||
            !Enum.TryParse(
                userRoleClaim,
                ignoreCase: true,
                out UserRole actorRole) ||
            !Enum.IsDefined(actorRole))
        {
            return Unauthorized();
        }

        TicketResponse? response =
            await _ticketService.AssignAsync(
                id,
                request.AssigneeId,
                actorId,
                actorRole,
                cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// Removes a Ticket's assignee within the actor's ownership scope.
    /// </summary>
    [HttpDelete("{id:guid}/assignee")]
    [Authorize(Policy = AuthorizationPolicies.AgentOrAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unassign(
        Guid id,
        CancellationToken cancellationToken)
    {
        string? userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        string? userRoleClaim =
            User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(userIdClaim, out Guid actorId) ||
            !Enum.TryParse(
                userRoleClaim,
                ignoreCase: true,
                out UserRole actorRole) ||
            !Enum.IsDefined(actorRole))
        {
            return Unauthorized();
        }

        TicketResponse? response =
            await _ticketService.UnassignAsync(
                id,
                actorId,
                actorRole,
                cancellationToken);

        return response is null ? NotFound() : NoContent();
    }

    /// <summary>
    /// Retrieves one visible Ticket's public Comments chronologically.
    /// </summary>
    [HttpGet("{id:guid}/comments")]
    [ProducesResponseType<IReadOnlyList<TicketCommentResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<TicketCommentResponse>>>
        GetComments(
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

        IReadOnlyList<TicketCommentResponse>? response =
            await _ticketService.GetCommentsAsync(
                id,
                viewerId,
                viewerRole,
                cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// Adds one public Comment to a visible Ticket.
    /// </summary>
    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType<TicketCommentResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketCommentResponse>> AddComment(
        Guid id,
        AddTicketCommentRequest request,
        CancellationToken cancellationToken)
    {
        string? userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        string? userRoleClaim =
            User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(userIdClaim, out Guid authorId) ||
            !Enum.TryParse(
                userRoleClaim,
                ignoreCase: true,
                out UserRole authorRole) ||
            !Enum.IsDefined(authorRole))
        {
            return Unauthorized();
        }

        TicketCommentResponse? response =
            await _ticketService.AddCommentAsync(
                id,
                authorId,
                authorRole,
                request,
                cancellationToken);

        return response is null
            ? NotFound()
            : Created(
                $"/tickets/{id}/comments/{response.Id}",
                response);
    }

    /// <summary>
    /// Reopens the requester's resolved Ticket with a public reason.
    /// </summary>
    [HttpPost("{id:guid}/reopen")]
    [ProducesResponseType<ReopenTicketResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReopenTicketResponse>> Reopen(
        Guid id,
        ReopenTicketRequest request,
        CancellationToken cancellationToken)
    {
        string? userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        string? userRoleClaim =
            User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(userIdClaim, out Guid requesterId) ||
            !Enum.TryParse(
                userRoleClaim,
                ignoreCase: true,
                out UserRole requesterRole) ||
            !Enum.IsDefined(requesterRole))
        {
            return Unauthorized();
        }

        ReopenTicketResponse? response =
            await _ticketService.ReopenAsync(
                id,
                requesterId,
                requesterRole,
                request,
                cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// Retrieves one visible Ticket's chronological status history.
    /// </summary>
    [HttpGet("{id:guid}/status-history")]
    [ProducesResponseType<IReadOnlyList<TicketStatusChangeResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<TicketStatusChangeResponse>>>
        GetStatusHistory(
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

        IReadOnlyList<TicketStatusChangeResponse>? response =
            await _ticketService.GetStatusHistoryAsync(
                id,
                viewerId,
                viewerRole,
                cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>
    /// Changes one Ticket's lifecycle status.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketResponse>> ChangeStatus(
        Guid id,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        string? userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        string? userRoleClaim =
            User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(userIdClaim, out Guid actorId) ||
            !Enum.TryParse(
                userRoleClaim,
                ignoreCase: true,
                out UserRole actorRole) ||
            !Enum.IsDefined(actorRole))
        {
            return Unauthorized();
        }

        TicketResponse? response =
            await _ticketService.ChangeStatusAsync(
                id,
                actorId,
                actorRole,
                request,
                cancellationToken);

        return response is null ? NotFound() : Ok(response);
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
