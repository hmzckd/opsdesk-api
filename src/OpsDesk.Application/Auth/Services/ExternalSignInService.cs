using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Services;

public sealed class ExternalSignInService(
    IExternalIdentityRepository identities, IInvitationRepository invitations,
    IJwtTokenGenerator jwt, IEmailValidator emailValidator,
    ExternalSignInPolicy policy, TimeProvider timeProvider) : IExternalSignInService
{
    // Enforces the approved provider/domain and invitation before issuing an OpsDesk JWT.
    public async Task<AuthResponse> SignInAsync(VerifiedExternalIdentity identity, string? invitationHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!policy.Enabled || identity.Issuer != policy.Issuer || !identity.EmailVerified
            || string.IsNullOrWhiteSpace(identity.Subject) || identity.Subject.Length > 255)
            throw new ForbiddenException("The external identity is not approved for OpsDesk.");
        emailValidator.Validate(identity.Email);
        string email = UserInputNormalizer.NormalizeEmail(identity.Email);
        string domain = email[(email.LastIndexOf('@') + 1)..];
        if (!policy.AllowedEmailDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            throw new ForbiddenException("The external identity is not approved for OpsDesk.");

        User? user = await identities.GetUserAsync(identity.Issuer, identity.Subject, cancellationToken);
        if (user is null)
        {
            UserInvitation? invitation = string.IsNullOrWhiteSpace(invitationHash) ? null
                : await invitations.GetByTokenHashAsync(invitationHash, cancellationToken);
            DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            if (invitation is null || invitation.Email != email || !invitation.CanAcceptAt(nowUtc))
                throw new ForbiddenException("A valid administrator invitation matching your verified email is required.");
            user = new User
            {
                FirstName = UserInputNormalizer.NormalizeName(identity.FirstName, nameof(identity.FirstName)),
                LastName = UserInputNormalizer.NormalizeName(identity.LastName, nameof(identity.LastName)),
                Email = email, Role = invitation.Role, PasswordHash = null, CreatedAtUtc = nowUtc
            };
            user.MarkEmailVerified(nowUtc);
            UserExternalIdentity link = UserExternalIdentity.Create(user.Id, identity.Issuer, identity.Subject, nowUtc);
            if (!await identities.TryAcceptInvitationAsync(invitation.Id, user, link, cancellationToken))
                throw new ForbiddenException("A valid administrator invitation matching your verified email is required.");
        }
        var token = jwt.GenerateToken(user);
        return new AuthResponse(user.Id, user.FirstName, user.LastName, user.Email, user.Role.ToString(),
            token.AccessToken, token.ExpiresAtUtc);
    }
}
