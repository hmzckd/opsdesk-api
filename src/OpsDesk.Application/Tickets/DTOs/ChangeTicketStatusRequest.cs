using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Contains the server-recognized status requested by the caller.
/// </summary>
public sealed record ChangeTicketStatusRequest(TicketStatus Status);
