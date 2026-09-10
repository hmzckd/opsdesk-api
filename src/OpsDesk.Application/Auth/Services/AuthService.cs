using System;
using System.Threading;
using System.Threading.Tasks;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Auth.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IEmailValidator _emailValidator;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly bool _publicRegistrationEnabled;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordValidator passwordValidator,
        IEmailValidator emailValidator,
        IEmailVerificationService emailVerificationService,
        RegistrationSettings registrationSettings)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordValidator = passwordValidator;
        _emailValidator = emailValidator;
        _emailVerificationService = emailVerificationService;
        _publicRegistrationEnabled = registrationSettings.PublicRegistrationEnabled;
    }

    // Rejects disabled self-registration before any account access; otherwise registers and sends verification.
    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_publicRegistrationEnabled)
        {
            throw new ForbiddenException("Public registration is disabled. Contact an administrator for an invitation.");
        }

        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        _emailValidator.Validate(request.Email);
        _passwordValidator.Validate(request.Password);

        string firstName = UserInputNormalizer.NormalizeName(
            request.FirstName,
            nameof(request.FirstName));

        string lastName = UserInputNormalizer.NormalizeName(
            request.LastName,
            nameof(request.LastName));

        string normalizedEmail =
            UserInputNormalizer.NormalizeEmail(request.Email);

        bool emailExists = await _userRepository.ExistsByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            throw new ConflictException(
                "A user with this email address already exists.");
        }

        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.Customer
        };

        await _userRepository.AddAsync(user, cancellationToken);

        await _emailVerificationService.IssueAsync(
            user,
            cancellationToken);

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        _emailValidator.Validate(request.Email);

        string normalizedEmail =
            UserInputNormalizer.NormalizeEmail(request.Email);

        User? user = await _userRepository.GetByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash) ||
            !_passwordHasher.VerifyPassword(
                request.Password,
                user.PasswordHash))
        {
            throw new UnauthorizedAccessException(
                "Email or password is incorrect.");
        }

        return CreateAuthResponse(user);
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        JwtTokenResult token =
            _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.Role.ToString(),
            token.AccessToken,
            token.ExpiresAtUtc);
    }
}
