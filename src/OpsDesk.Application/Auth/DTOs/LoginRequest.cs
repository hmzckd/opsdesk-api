namespace OpsDesk.Application.Auth.DTOs;

public sealed record LoginRequest(
    string Email,
    string Password);