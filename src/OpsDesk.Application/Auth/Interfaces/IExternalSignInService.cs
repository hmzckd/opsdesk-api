using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IExternalSignInService
{
    // Resolves a trusted identity or atomically accepts a matching invitation on first sign-in.
    Task<AuthResponse> SignInAsync(VerifiedExternalIdentity identity, string? invitationHash,
        CancellationToken cancellationToken = default);
}
