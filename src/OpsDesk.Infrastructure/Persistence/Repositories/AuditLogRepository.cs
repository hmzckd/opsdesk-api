using Microsoft.EntityFrameworkCore;
using OpsDesk.Application.Audit.DTOs;
using OpsDesk.Application.Audit.Interfaces;
using OpsDesk.Application.Audit.Queries;
using OpsDesk.Application.Common.Pagination;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(OpsDeskDbContext database)
    : IAuditLogRepository
{
    /// <summary>
    /// Uses the same scoped DbContext as the business repository.
    /// </summary>
    public void Stage(AuditLog entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        database.AuditLogs.Add(entry);
    }

    /// <summary>
    /// Projects only safe fields and applies stable ordering in PostgreSQL.
    /// </summary>
    public async Task<PagedResponse<AuditLogResponse>> GetPageAsync(
        AuditLogQuery filters,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AuditLog> query = database.AuditLogs.AsNoTracking();

        if (filters.Action is AuditAction action)
            query = query.Where(entry => entry.Action == action);
        if (filters.ActorId is Guid actorId)
            query = query.Where(entry => entry.ActorId == actorId);
        if (filters.TargetType is AuditTargetType targetType)
            query = query.Where(entry => entry.TargetType == targetType);
        if (filters.TargetId is Guid targetId)
            query = query.Where(entry => entry.TargetId == targetId);
        if (filters.FromUtc is DateTime fromUtc)
            query = query.Where(entry => entry.OccurredAtUtc >= fromUtc);
        if (filters.ToUtc is DateTime toUtc)
            query = query.Where(entry => entry.OccurredAtUtc < toUtc);

        int totalCount = await query.CountAsync(cancellationToken);
        long offset = ((long)filters.Page - 1) * filters.PageSize;
        List<AuditLogResponse> items = offset >= totalCount
            ? []
            : await query
                .OrderByDescending(entry => entry.OccurredAtUtc)
                .ThenByDescending(entry => entry.Id)
                .Skip((int)offset)
                .Take(filters.PageSize)
                .Select(entry => new AuditLogResponse(
                    entry.Id, entry.Action, entry.ActorId,
                    entry.TargetType, entry.TargetId, entry.OccurredAtUtc,
                    entry.PreviousStatus, entry.NewStatus,
                    entry.PreviousAssigneeId, entry.NewAssigneeId))
                .ToListAsync(cancellationToken);

        int totalPages = totalCount == 0 ? 0
            : (int)Math.Ceiling(totalCount / (double)filters.PageSize);

        return new PagedResponse<AuditLogResponse>(
            items, filters.Page, filters.PageSize, totalCount, totalPages,
            filters.Page > 1 && totalPages > 0,
            filters.Page < totalPages);
    }
}
