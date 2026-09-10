using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Invitations.DTOs;

public sealed record AcceptedInvitationResponse(
    Guid UserId, string Email, UserRole Role, DateTime EmailVerifiedAtUtc);
