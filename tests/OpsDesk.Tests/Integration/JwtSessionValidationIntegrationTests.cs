using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Authorization;
using OpsDesk.Infrastructure.Authentication;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class JwtSessionValidationIntegrationTests(OpsDeskApiFixture fixture)
{
    // Valid signatures alone cannot authorize legacy, malformed, duplicate, or mismatched session versions.
    [Theory]
    [InlineData(null, false)]
    [InlineData("invalid", false)]
    [InlineData("-1", false)]
    [InlineData("1", false)]
    [InlineData("0", true)]
    public async Task Invalid_session_versions_should_be_rejected_by_all_protected_routes(string? version, bool duplicate)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse session = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        var reader = new JwtSecurityTokenHandler();
        List<Claim> claims = reader.ReadJwtToken(session.AccessToken).Claims
            .Where(claim => claim.Type != AuthClaimTypes.AuthVersion && claim.Type is not ("aud" or "iss" or "exp" or "nbf"))
            .ToList();
        if (version is not null) claims.Add(new Claim(AuthClaimTypes.AuthVersion, version));
        if (duplicate) claims.Add(new Claim(AuthClaimTypes.AuthVersion, version!));
        JwtSettings settings = fixture.Factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reader.WriteToken(token));
        foreach (string route in new[] { "/me", "/admin/access", "/tickets" })
        {
            using HttpResponseMessage rejected = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using HttpResponseMessage valid = await client.GetAsync("/admin/access");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }
}
