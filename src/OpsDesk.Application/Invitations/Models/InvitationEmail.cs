using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Invitations.Models;

public sealed record InvitationEmail(
    string RecipientEmail, UserRole Role, string RawToken, DateTime ExpiresAtUtc);
