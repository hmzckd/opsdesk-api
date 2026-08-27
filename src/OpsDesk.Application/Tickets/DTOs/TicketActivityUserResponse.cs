using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Provides the safe User details needed to understand a timeline item.
/// </summary>
public sealed record TicketActivityUserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    UserRole Role);
