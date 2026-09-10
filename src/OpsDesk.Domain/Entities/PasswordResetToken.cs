namespace OpsDesk.Domain.Entities;

public sealed class PasswordResetToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }

    private PasswordResetToken() { }

    // Rejects expired, revoked, consumed, or not-yet-valid credentials without changing state.
    public bool CanUseAt(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Reset time must be UTC.", nameof(nowUtc));
        return RevokedAtUtc is null && ConsumedAtUtc is null && CreatedAtUtc <= nowUtc && nowUtc < ExpiresAtUtc;
    }

    // Marks a valid token used; persistence combines this with the password and session version update.
    public void Consume(DateTime nowUtc)
    {
        if (!CanUseAt(nowUtc)) throw new ArgumentException("Password reset token is invalid or expired.");
        ConsumedAtUtc = nowUtc;
    }

    // Creates a purpose-specific reset credential with the approved thirty-minute lifetime.
    public static PasswordResetToken Create(Guid userId, string tokenHash, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (userId == Guid.Empty || tokenHash.Length != 64 || createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A reset token requires a User, SHA-256 hash, and UTC creation time.");
        return new PasswordResetToken
        {
            Id = Guid.NewGuid(), UserId = userId, TokenHash = tokenHash,
            CreatedAtUtc = createdAtUtc, ExpiresAtUtc = createdAtUtc.AddMinutes(30)
        };
    }
}
