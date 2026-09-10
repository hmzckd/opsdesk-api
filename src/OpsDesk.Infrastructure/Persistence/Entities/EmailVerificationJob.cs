namespace OpsDesk.Infrastructure.Persistence.Entities;

// Queue bookkeeping belongs to persistence and never stores a raw token.
public sealed class EmailVerificationJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public int Attempts { get; set; }
}
