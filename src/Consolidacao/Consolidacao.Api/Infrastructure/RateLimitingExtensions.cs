using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Consolidacao.Api.Infrastructure;

public static class RateLimitingExtensions
{
    public const string SaldosPolicy = "saldos";

    /// <summary>
    /// Token bucket global para as consultas: sustenta 50 req/s com folga (reposição de 100/s, rajada de 200)
    /// e protege o serviço de picos muito acima do contratado respondendo 429 + Retry-After
    /// em vez de degradar todas as requisições.
    /// </summary>
    public static IServiceCollection AddSaldosRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var tokenLimit = configuration.GetValue("RateLimiting:TokenLimit", 200);
        var tokensPerSecond = configuration.GetValue("RateLimiting:TokensPerSecond", 100);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(SaldosPolicy, _ => RateLimitPartition.GetTokenBucketLimiter("saldos", _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = tokenLimit,
                TokensPerPeriod = tokensPerSecond,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
            options.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "1";
                return ValueTask.CompletedTask;
            };
        });

        return services;
    }
}
