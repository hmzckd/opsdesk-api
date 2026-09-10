namespace OpsDesk.Application.Auth.Models;

public sealed record PasswordRecoveryWorkItem(
    Guid Id, string Email, DateTime RequestedAtUtc, Guid LeaseId, int Attempts);
