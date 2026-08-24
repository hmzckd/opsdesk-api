using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Contains the client-controlled fields accepted when creating a Ticket.
/// </summary>
public sealed record CreateTicketRequest
{
    /// <summary>
    /// Creates the request contract from client-controlled Ticket fields.
    /// </summary>
    [JsonConstructor]
    public CreateTicketRequest(
        string? title,
        string? description,
        TicketPriority? priority = null)
    {
        Title = title;
        Description = description;
        Priority = priority;
    }

    [Required]
    [StringLength(Ticket.MaximumTitleLength)]
    public string? Title { get; init; }

    [Required]
    [StringLength(Ticket.MaximumDescriptionLength)]
    public string? Description { get; init; }

    public TicketPriority? Priority { get; init; }
}
