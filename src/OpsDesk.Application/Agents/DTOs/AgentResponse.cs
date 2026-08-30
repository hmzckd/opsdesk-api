namespace OpsDesk.Application.Agents.DTOs;

/// <summary>
/// Exposes safe Agent identity data without authentication secrets.
/// </summary>
public sealed record AgentResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    DateTime CreatedAtUtc);
