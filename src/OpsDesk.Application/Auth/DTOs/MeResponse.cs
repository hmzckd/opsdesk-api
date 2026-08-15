using System;

namespace OpsDesk.Application.Auth.DTOs;

public sealed record MeResponse(
    Guid UserId,
    string Name,
    string Email,
    string Role);