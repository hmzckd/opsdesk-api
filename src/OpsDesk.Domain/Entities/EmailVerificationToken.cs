namespace OpsDesk.Domain.Entities;

public sealed class EmailVerificationToken
{
    private EmailVerificationToken()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Creates a stored representation of one email verification token.
    /// </summary>
    public static EmailVerificationToken Create(
        Guid userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException(
                "Token hash cannot be empty.",
                nameof(tokenHash));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Token creation time must be UTC.",
                nameof(createdAtUtc));
        }

        if (expiresAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Token expiration time must be UTC.",
                nameof(expiresAtUtc));
        }

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException(
                "Token expiration time must be after its creation time.",
                nameof(expiresAtUtc));
        }

        return new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    /// <summary>
    /// Reports whether the token has reached its expiration time.
    /// </summary>
    public bool IsExpiredAt(DateTime checkedAtUtc)
    {
        if (checkedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Token check time must be UTC.",
                nameof(checkedAtUtc));
        }

        return checkedAtUtc >= ExpiresAtUtc;
    }
}
