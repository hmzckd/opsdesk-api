using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Services;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Invitations.DTOs;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Invitations.Models;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Invitations.Services;

public sealed class InvitationService(
    IUserRepository users,
    IInvitationRepository invitations,
    IInvitationTokenGenerator tokens,
    IInvitationEmailSender emailSender,
    IEmailValidator emailValidator,
    IPasswordValidator passwordValidator,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : IInvitationService
{
    // Validates recipient input, binds identity to the invitation, and atomically creates the account.
    public async Task<AcceptedInvitationResponse> AcceptAsync(
        AcceptInvitationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string hash = tokens.ComputeHash(request.Token);
        UserInvitation? invitation = await invitations.GetByTokenHashAsync(hash, cancellationToken);
        if (invitation is null || !invitation.CanAcceptAt(timeProvider.GetUtcNow().UtcDateTime))
        {
            throw new ArgumentException("Invitation token is invalid or expired.");
        }

        passwordValidator.Validate(request.Password);
        string firstName = UserInputNormalizer.NormalizeName(request.FirstName, nameof(request.FirstName));
        string lastName = UserInputNormalizer.NormalizeName(request.LastName, nameof(request.LastName));
        string passwordHash = passwordHasher.HashPassword(request.Password);
        DateTime acceptedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (!invitation.CanAcceptAt(acceptedAtUtc))
        {
            throw new ArgumentException("Invitation token is invalid or expired.");
        }

        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = invitation.Email,
            Role = invitation.Role,
            PasswordHash = passwordHash,
            CreatedAtUtc = acceptedAtUtc
        };
        user.MarkEmailVerified(acceptedAtUtc);

        bool accepted = await invitations.TryAcceptAsync(invitation.Id, user, acceptedAtUtc, cancellationToken);
        if (!accepted)
        {
            throw new ArgumentException("Invitation token is invalid or expired.");
        }

        return new AcceptedInvitationResponse(user.Id, user.Email, user.Role, acceptedAtUtc);
    }

    // Validates the actor and recipient, stores a pending invitation, then delivers it.
    public async Task<InvitationResponse> CreateAsync(
        CreateInvitationRequest request, Guid invitedById,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        User? inviter = await users.GetByIdAsync(invitedById, cancellationToken);
        if (inviter is not { Role: UserRole.Admin })
        {
            throw new ForbiddenException("Only an Admin can invite users.");
        }

        emailValidator.Validate(request.Email);
        string email = UserInputNormalizer.NormalizeEmail(request.Email);
        if (await users.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        GeneratedInvitationToken token = tokens.GenerateToken();
        UserInvitation invitation = UserInvitation.Create(
            email, request.Role, invitedById, token.TokenHash,
            timeProvider.GetUtcNow().UtcDateTime);
        await invitations.AddAsync(invitation, cancellationToken);

        try
        {
            await emailSender.SendAsync(new InvitationEmail(
                email, invitation.Role, token.RawToken, invitation.ExpiresAtUtc),
                cancellationToken);
        }
        catch
        {
            // A failed delivery must not leave an active invitation blocking a retry.
            await invitations.RevokeAsync(invitation.Id,
                timeProvider.GetUtcNow().UtcDateTime, CancellationToken.None);
            throw;
        }

        return new InvitationResponse(invitation.Id, invitation.Email,
            invitation.Role, invitation.InvitedById,
            invitation.CreatedAtUtc, invitation.ExpiresAtUtc);
    }
}
