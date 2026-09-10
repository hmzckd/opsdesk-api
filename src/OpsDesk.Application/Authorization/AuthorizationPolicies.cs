namespace OpsDesk.Application.Authorization;

public static class AuthorizationPolicies
{
    public const string VerifiedEmail = "VerifiedEmail";

    public const string AdminOnly = "AdminOnly";

    public const string AgentOrAdmin = "AgentOrAdmin";
}
