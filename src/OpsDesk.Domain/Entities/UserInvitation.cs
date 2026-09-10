using OpsDesk.Domain.Enums;

namespace OpsDesk.Domain.Entities;

public sealed class UserInvitation
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public Guid InvitedById { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public Guid? AcceptedUserId { get; private set; }

    private UserInvitation() { }

    // A revoked, consumed, not-yet-valid, or expired invitation cannot create an account.
    public bool CanAcceptAt(DateTime acceptedAtUtc)
    {
        if (acceptedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Acceptance time must be UTC.", nameof(acceptedAtUtc));
        }

        return RevokedAtUtc is null && AcceptedAtUtc is null
            && acceptedAtUtc >= CreatedAtUtc && acceptedAtUtc < ExpiresAtUtc;
    }

    // Creates a pending invitation with the role and lifetime fixed by server rules.
    public static UserInvitation Create(
        string email, UserRole role, Guid invitedById,
        string tokenHash, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (email.Length > 254 || tokenHash.Length != 64)
        {
            throw new ArgumentException("Invitation email or token hash is invalid.");
        }

        if (role is not (UserRole.Customer or UserRole.Agent))
        {
            throw new ArgumentException("Only Customer and Agent invitations are allowed.", nameof(role));
        }

        if (invitedById == Guid.Empty || createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Invitation requires an inviter and a UTC creation time.");
        }

        return new UserInvitation
        {
            Id = Guid.NewGuid(),
            Email = email,
            Role = role,
            InvitedById = invitedById,
            TokenHash = tokenHash,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.AddHours(24)
        };
    }
}
