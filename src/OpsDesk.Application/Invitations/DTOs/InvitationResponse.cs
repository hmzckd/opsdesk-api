using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Invitations.DTOs;

public sealed record InvitationResponse(
    Guid Id, string Email, UserRole Role, Guid InvitedById,
    DateTime CreatedAtUtc, DateTime ExpiresAtUtc);
