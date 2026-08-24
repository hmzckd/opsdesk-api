using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Contains the client-controlled content of a new Ticket Comment.
/// </summary>
public sealed record AddTicketCommentRequest
{
    /// <summary>
    /// Creates the request without accepting author or timestamp fields.
    /// </summary>
    [JsonConstructor]
    public AddTicketCommentRequest(string? content)
    {
        Content = content;
    }

    [Required]
    [StringLength(TicketComment.MaximumContentLength)]
    public string? Content { get; init; }
}
