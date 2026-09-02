using System.ComponentModel.DataAnnotations;

namespace OpsDesk.Application.Tickets.DTOs;

/// <summary>
/// Contains client-controlled paging, filtering, and sorting values.
/// </summary>
public sealed record ListTicketsRequest
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;
    public const string DefaultSortBy = "createdAtUtc";
    public const string DefaultSortDirection = "desc";

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = DefaultPage;

    [Range(1, MaximumPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;

    public string? Status { get; init; }

    public string? Priority { get; init; }

    public Guid? RequesterId { get; init; }

    public Guid? AssigneeId { get; init; }

    public bool Unassigned { get; init; }

    public string SortBy { get; init; } = DefaultSortBy;

    public string SortDirection { get; init; } =
        DefaultSortDirection;
}
