using System;

namespace OpsDesk.Application.Auth.DTOs;

public sealed record AuthResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    string AccessToken,
    DateTime ExpiresAtUtc);