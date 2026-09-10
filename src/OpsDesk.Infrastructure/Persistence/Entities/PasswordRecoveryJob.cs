namespace OpsDesk.Infrastructure.Persistence.Entities;

// Queue bookkeeping is a persistence detail, not a business entity.
public sealed class PasswordRecoveryJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public int Attempts { get; set; }
}
