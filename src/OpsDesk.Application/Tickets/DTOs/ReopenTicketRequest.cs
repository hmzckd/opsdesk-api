using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Contains the requester-controlled reason for reopening a Ticket.
/// </summary>
public sealed record ReopenTicketRequest
{
    /// <summary>
    /// Creates the request without accepting actor or timestamp fields.
    /// </summary>
    [JsonConstructor]
    public ReopenTicketRequest(string? reason)
    {
        Reason = reason;
    }

    [Required]
    [StringLength(TicketComment.MaximumContentLength)]
    public string? Reason { get; init; }
}
