using System.ComponentModel.DataAnnotations;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Contains client-controlled pagination values for Ticket listing.
/// </summary>
public sealed record ListTicketsRequest
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = DefaultPage;

    [Range(1, MaximumPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;
}
