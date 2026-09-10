using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Application.Invitations.Interfaces;

public interface IInvitationEmailSender
{
    // Delivers the raw invitation token to its intended recipient.
    Task SendAsync(InvitationEmail email, CancellationToken cancellationToken = default);
}
