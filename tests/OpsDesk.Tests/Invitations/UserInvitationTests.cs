using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Tests.Invitations;

public sealed class UserInvitationTests
{
    [Fact]
    public void Newly_created_invitation_cannot_be_accepted_before_email_is_sent()
    {
        DateTime now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
        UserInvitation invitation = UserInvitation.Create(
            "recipient@example.com", UserRole.Agent, Guid.NewGuid(), new string('a', 64), now);

        Assert.False(invitation.CanAcceptAt(now));
    }
}
