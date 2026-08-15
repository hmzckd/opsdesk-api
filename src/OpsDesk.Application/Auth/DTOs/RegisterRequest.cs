namespace OpsDesk.Application.Auth.DTOs;

public sealed record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password);