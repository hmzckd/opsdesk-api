using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Models;

public sealed record EmailVerificationTokenReplacement(
    bool Replaced,
    EmailVerificationToken? PreviousToken);
