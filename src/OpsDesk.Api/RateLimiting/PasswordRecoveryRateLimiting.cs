using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace OpsDesk.Api.RateLimiting;

public static class PasswordRecoveryRateLimiting
{
    public const string RequestPolicy = "password-recovery-request";
    public const string ResetPolicy = "password-recovery-reset";

    // Registers an endpoint-specific, per-IP limit; account existence never influences the partition.
    public static IServiceCollection AddPasswordRecoveryRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            int permitLimit = configuration.GetValue("PasswordRecovery:RateLimits:RequestPermitLimit", 5);
            int resetLimit = configuration.GetValue("PasswordRecovery:RateLimits:ResetPermitLimit", 10);
            int windowMinutes = configuration.GetValue("PasswordRecovery:RateLimits:WindowMinutes", 15);
            if (permitLimit is < 1 or > 1000 || resetLimit is < 1 or > 1000 || windowMinutes is < 1 or > 1440)
                throw new InvalidOperationException("Password recovery rate limits are outside their supported range.");
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RequestPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(windowMinutes),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.AddPolicy(ResetPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = resetLimit,
                    Window = TimeSpan.FromMinutes(windowMinutes),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });
        return services;
    }
}
