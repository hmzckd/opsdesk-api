namespace OpsDesk.Api.Models;

public sealed record SsoLogoutResponse(
    bool LocalSessionsRevoked,
    string? ProviderLogoutUrl);
