namespace OpsDesk.Api.Authentication;

public static class SsoAuthenticationSchemes
{
    public const string OpenIdConnect = "OpsDesk.Sso.OpenIdConnect";

    public const string TemporaryCookie = "OpsDesk.Sso.TemporaryCookie";

    public const string InvitationHashItem = "opsdesk.sso.invitation_hash";

    public const string IssuerItem = "opsdesk.sso.issuer";
}
