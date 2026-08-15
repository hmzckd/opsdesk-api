using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        AuthResponse response =
            await _authService.RegisterAsync(
                request,
                cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        AuthResponse response =
            await _authService.LoginAsync(
                request,
                cancellationToken);

        return Ok(response);
    }

    [Authorize]
    [HttpGet("/me")]
    public ActionResult<MeResponse> Me()
    {
        string? userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out Guid userId))
        {
            return Unauthorized();
        }

        var response = new MeResponse(
            userId,
            User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? string.Empty,
            User.FindFirstValue(ClaimTypes.Role) ?? string.Empty);

        return Ok(response);
    }
}