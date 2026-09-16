using OpsDesk.Application.Agents.DTOs;
using OpsDesk.Application.Agents.Interfaces;
using OpsDesk.Application.Audit.Interfaces;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Services;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Agents.Services;

public sealed class AgentService : IAgentService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IEmailValidator _emailValidator;
    private readonly IAuditLogRepository _auditLogs;

    public AgentService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator,
        IEmailValidator emailValidator,
        IAuditLogRepository auditLogs)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
        _emailValidator = emailValidator;
        _auditLogs = auditLogs;
    }

    /// <summary>
    /// Creates an Agent while keeping role, password hash, ID, and time
    /// under server control.
    /// </summary>
    public async Task<AgentResponse> CreateAsync(
        CreateAgentRequest request,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        _emailValidator.Validate(request.Email);
        _passwordValidator.Validate(request.Password);

        string firstName = UserInputNormalizer.NormalizeName(
            request.FirstName,
            nameof(request.FirstName));

        string lastName = UserInputNormalizer.NormalizeName(
            request.LastName,
            nameof(request.LastName));

        string normalizedEmail =
            UserInputNormalizer.NormalizeEmail(request.Email);

        bool emailExists = await _userRepository.ExistsByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            throw new ConflictException(
                "A user with this email address already exists.");
        }

        var agent = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(
                request.Password),
            Role = UserRole.Agent
        };

        _auditLogs.Stage(AuditLog.ForAgentCreated(
            agent.Id, actorId, agent.CreatedAtUtc));

        await _userRepository.AddAsync(
            agent,
            cancellationToken);

        return MapToResponse(agent);
    }

    /// <summary>
    /// Maps a User entity to the API-safe Agent contract.
    /// </summary>
    private static AgentResponse MapToResponse(User agent)
    {
        return new AgentResponse(
            agent.Id,
            agent.FirstName,
            agent.LastName,
            agent.Email,
            agent.Role.ToString(),
            agent.CreatedAtUtc);
    }
}
