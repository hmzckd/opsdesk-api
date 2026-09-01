namespace OpsDesk.Application.Common.Pagination;

/// <summary>
/// Represents one bounded page and its collection metadata.
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);
