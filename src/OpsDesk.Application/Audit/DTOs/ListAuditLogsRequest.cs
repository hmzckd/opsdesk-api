using System.ComponentModel.DataAnnotations;

namespace OpsDesk.Application.Audit.DTOs;

/// <summary>
/// Collects the Admin's bounded audit filters from the query string.
/// </summary>
public sealed record ListAuditLogsRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public string? Action { get; init; }

    public Guid? ActorId { get; init; }

    public string? TargetType { get; init; }

    public Guid? TargetId { get; init; }

    public string? FromUtc { get; init; }

    public string? ToUtc { get; init; }
}
