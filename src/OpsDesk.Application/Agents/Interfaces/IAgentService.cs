using OpsDesk.Application.Agents.DTOs;

namespace OpsDesk.Application.Agents.Interfaces;

public interface IAgentService
{
    /// <summary>
    /// Validates and persists one server-owned Agent identity.
    /// </summary>
    Task<AgentResponse> CreateAsync(
        CreateAgentRequest request,
        CancellationToken cancellationToken = default);
}
