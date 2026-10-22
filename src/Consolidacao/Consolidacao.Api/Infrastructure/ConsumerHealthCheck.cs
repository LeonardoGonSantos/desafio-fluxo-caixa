using Consolidacao.External.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Consolidacao.Api.Infrastructure;

/// <summary>Consumo parado degrada (não derruba) o serviço: as consultas continuam respondendo com o último saldo.</summary>
public sealed class ConsumerHealthCheck(ConsumerStatus status) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(status.IsConsuming
            ? HealthCheckResult.Healthy("Consumindo mensagens.")
            : HealthCheckResult.Degraded("Consumo de mensagens parado; o saldo pode ficar defasado."));
}
