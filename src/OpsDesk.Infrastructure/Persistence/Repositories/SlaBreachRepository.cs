using Microsoft.EntityFrameworkCore;
using OpsDesk.Application.Sla.Interfaces;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class SlaBreachRepository : ISlaBreachRepository
{
    private readonly OpsDeskDbContext _dbContext;

    public SlaBreachRepository(OpsDeskDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Records the oldest eligible breaches in one concurrency-safe SQL command.
    /// </summary>
    public Task<int> RecordNewBreachesAsync(
        DateTime detectedAtUtc,
        int maximumCandidates,
        CancellationToken cancellationToken = default)
    {
        if (detectedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "SLA breach detection time must be UTC.",
                nameof(detectedAtUtc));
        }

        FormattableString command = $"""
            WITH candidates AS (
                SELECT
                    ticket.id,
                    ticket.sla_policy_id,
                    ticket.sla_deadline_utc
                FROM tickets AS ticket
                WHERE ticket.sla_deadline_utc < {detectedAtUtc}
                  AND (
                      ticket.resolved_at_utc IS NULL
                      OR ticket.resolved_at_utc > ticket.sla_deadline_utc)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM ticket_sla_breaches AS breach
                      WHERE breach.ticket_id = ticket.id)
                ORDER BY ticket.sla_deadline_utc, ticket.id
                LIMIT {maximumCandidates}
                FOR UPDATE SKIP LOCKED
            )
            INSERT INTO ticket_sla_breaches (
                ticket_id,
                sla_policy_id,
                sla_deadline_utc,
                detected_at_utc)
            SELECT
                candidate.id,
                candidate.sla_policy_id,
                candidate.sla_deadline_utc,
                {detectedAtUtc}
            FROM candidates AS candidate
            ON CONFLICT (ticket_id) DO NOTHING;
            """;

        return _dbContext.Database.ExecuteSqlInterpolatedAsync(
            command,
            cancellationToken);
    }
}
