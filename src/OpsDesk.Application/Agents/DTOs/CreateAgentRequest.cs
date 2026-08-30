namespace OpsDesk.Application.Agents.DTOs;

/// <summary>
/// Carries the identity and initial credentials for a new Agent.
/// </summary>
public sealed record CreateAgentRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password);
