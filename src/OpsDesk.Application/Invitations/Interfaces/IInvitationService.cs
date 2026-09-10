using OpsDesk.Application.Invitations.DTOs;

namespace OpsDesk.Application.Invitations.Interfaces;

public interface IInvitationService
{
    // Creates a verified account with the email and role stored in a valid invitation.
    Task<AcceptedInvitationResponse> AcceptAsync(
        AcceptInvitationRequest request, CancellationToken cancellationToken = default);

    // Creates one invitation for the authenticated inviter and sends its token by email.
    Task<InvitationResponse> CreateAsync(
        CreateInvitationRequest request, Guid invitedById,
        CancellationToken cancellationToken = default);
}
