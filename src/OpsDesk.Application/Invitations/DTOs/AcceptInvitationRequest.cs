namespace OpsDesk.Application.Invitations.DTOs;

public sealed record AcceptInvitationRequest(
    string Token, string FirstName, string LastName, string Password);
