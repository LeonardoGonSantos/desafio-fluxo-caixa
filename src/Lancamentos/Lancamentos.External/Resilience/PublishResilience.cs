using System.ComponentModel.DataAnnotations;
using Lancamentos.External.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Lancamentos.External.Resilience;

public sealed class PublishResilienceOptions
{
    public const string SectionName = "Resilience:Publish";

    /// <summary>Tempo máximo de uma publicação (inclui o confirm do broker).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    [Range(0.01, 1.0)]
    public double FailureRatio { get; set; } = 0.5;

    [Range(2, 1000)]
    public int MinimumThroughput { get; set; } = 4;
}

/// <summary>
/// Política de resiliência da publicação no broker: circuit breaker (externo) + timeout (interno).
/// Não há retry aqui de propósito: quem repete é o OutboxProcessor, que é durável (sobrevive a restart)
/// e já aplica backoff. Dois retries empilhados só multiplicariam a latência sob falha.
/// </summary>
public static class PublishResilience
{
    public const string PipelineName = "rabbitmq-publish";

    /// <summary>
    /// Mensagem sem rota não é falha de infraestrutura: o broker está saudável, só falta a fila.
    /// Cancelamento do chamador também não deve abrir o circuito.
    /// </summary>
    public static bool IsInfrastructureFailure(Exception exception) =>
        exception is not OperationCanceledException and not UnroutableMessageException;

    public static void Configure(ResiliencePipelineBuilder builder, PublishResilienceOptions options)
    {
        builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(IsInfrastructureFailure),
                FailureRatio = options.FailureRatio,
                MinimumThroughput = options.MinimumThroughput,
                SamplingDuration = options.SamplingDuration,
                BreakDuration = options.BreakDuration,
            })
            .AddTimeout(new TimeoutStrategyOptions { Timeout = options.Timeout });
    }

    public static IServiceCollection AddPublishResilience(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PublishResilienceOptions>()
            .Bind(configuration.GetSection(PublishResilienceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddResiliencePipeline(PipelineName, (builder, context) =>
            Configure(builder, context.ServiceProvider.GetRequiredService<IOptions<PublishResilienceOptions>>().Value));

        return services;
    }
}
