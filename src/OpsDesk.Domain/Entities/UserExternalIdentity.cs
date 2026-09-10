namespace OpsDesk.Domain.Entities;

public sealed class UserExternalIdentity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Issuer { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private UserExternalIdentity() { }

    // Binds a provider's stable subject to one local user; email is deliberately not the identity key.
    public static UserExternalIdentity Create(Guid userId, string issuer, string subject, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (userId == Guid.Empty || issuer.Length > 512 || subject.Length > 255
            || createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("External identity requires a user, bounded issuer/subject and UTC time.");
        return new UserExternalIdentity
        {
            Id = Guid.NewGuid(), UserId = userId, Issuer = issuer, Subject = subject, CreatedAtUtc = createdAtUtc
        };
    }
}
