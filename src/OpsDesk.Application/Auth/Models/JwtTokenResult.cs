using System;

namespace OpsDesk.Application.Auth.Models;

public sealed record JwtTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc);