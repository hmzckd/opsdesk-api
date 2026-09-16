using System.Globalization;
using System.Text.Json;
using OpsDesk.Application.Audit.DTOs;
using OpsDesk.Application.Audit.Interfaces;
using OpsDesk.Application.Audit.Queries;
using OpsDesk.Application.Common.Pagination;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Audit.Services;

public sealed class AuditLogService(IAuditLogRepository auditLogs)
    : IAuditLogService
{
    /// <summary>
    /// Converts public filters to validated, UTC-only repository values.
    /// </summary>
    public Task<PagedResponse<AuditLogResponse>> ListAsync(
        ListAuditLogsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
        {
            throw new ArgumentException("Page or pageSize is outside the supported range.");
        }

        if (request.ActorId == Guid.Empty || request.TargetId == Guid.Empty)
        {
            throw new ArgumentException("Audit filter IDs cannot be empty.");
        }

        if (request.TargetId.HasValue && request.TargetType is null)
        {
            throw new ArgumentException(
                "targetType is required when targetId is supplied.",
                nameof(request.TargetType));
        }

        DateTime? fromUtc = ParseUtc(request.FromUtc, nameof(request.FromUtc));
        DateTime? toUtc = ParseUtc(request.ToUtc, nameof(request.ToUtc));
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc >= toUtc)
        {
            throw new ArgumentException("fromUtc must precede toUtc.");
        }

        var query = new AuditLogQuery(
            request.Page,
            request.PageSize,
            ParseOptionalEnum<AuditAction>(request.Action, nameof(request.Action)),
            request.ActorId,
            ParseOptionalEnum<AuditTargetType>(request.TargetType, nameof(request.TargetType)),
            request.TargetId,
            fromUtc,
            toUtc);

        return auditLogs.GetPageAsync(query, cancellationToken);
    }

    private static DateTime? ParseUtc(string? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        bool explicitUtc = value.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("+00:00", StringComparison.Ordinal);
        if (!explicitUtc ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTimeOffset parsed) ||
            parsed.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"{parameterName} must be an explicit UTC timestamp.",
                parameterName);
        }

        return parsed.UtcDateTime;
    }

    private static TEnum? ParseOptionalEnum<TEnum>(
        string? value, string parameterName) where TEnum : struct, Enum
    {
        if (value is null)
        {
            return null;
        }

        foreach (TEnum candidate in Enum.GetValues<TEnum>())
        {
            string queryValue = JsonNamingPolicy.SnakeCaseLower.ConvertName(
                candidate.ToString());
            if (string.Equals(value, queryValue, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new ArgumentException($"{parameterName} is not supported.", parameterName);
    }
}
