using System.Threading;
using System.Threading.Tasks;
using OpsDesk.Application.Auth.DTOs;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}