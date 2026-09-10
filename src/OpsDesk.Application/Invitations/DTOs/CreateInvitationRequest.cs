using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Invitations.DTOs;

public sealed record CreateInvitationRequest(string Email, UserRole Role);
