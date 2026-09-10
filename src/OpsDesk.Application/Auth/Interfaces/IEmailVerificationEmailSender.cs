using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IEmailVerificationEmailSender
{
    /// <summary>
    /// Delivers one email without storing or logging its raw token.
    /// </summary>
    Task SendAsync(
        EmailVerificationEmail email,
        CancellationToken cancellationToken = default);
}
