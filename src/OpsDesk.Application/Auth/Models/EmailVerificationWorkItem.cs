namespace OpsDesk.Application.Auth.Models;

public sealed record EmailVerificationWorkItem(
    Guid Id,
    string Email,
    DateTime RequestedAtUtc,
    Guid LeaseId,
    int Attempts);
