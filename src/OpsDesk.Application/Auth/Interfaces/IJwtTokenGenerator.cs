using OpsDesk.Application.Auth.Models;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IJwtTokenGenerator
{
    JwtTokenResult GenerateToken(User user);
}