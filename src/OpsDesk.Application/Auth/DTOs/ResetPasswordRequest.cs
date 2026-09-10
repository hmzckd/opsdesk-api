namespace OpsDesk.Application.Auth.DTOs;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
