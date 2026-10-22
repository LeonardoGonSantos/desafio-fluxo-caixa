using FluxoCaixa.BuildingBlocks.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FluxoCaixa.BuildingBlocks.Api;

/// <summary>
/// Broker indisponível degrada, mas não derruba, o serviço: lançamentos continuam sendo aceitos
/// e ficam no outbox até o broker voltar.
/// </summary>
public sealed class RabbitMqHealthCheck(RabbitMqConnectionProvider connectionProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await connectionProvider.GetConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("RabbitMQ indisponível; eventos acumulam no outbox.", ex);
        }
    }
}
