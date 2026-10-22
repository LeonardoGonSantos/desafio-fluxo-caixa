using Lancamentos.External.Messaging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Lancamentos.External.Resilience;

public static class ResiliencePipelines
{
    /// <summary>
    /// Retry exponencial com jitter + circuit breaker para a publicação no broker.
    /// Mensagem sem rota não é falha de infraestrutura: não é repetida nem abre o circuito.
    /// </summary>
    public static ResiliencePipeline CreatePublishPipeline(TimeProvider? timeProvider = null)
    {
        var shouldHandle = new PredicateBuilder()
            .Handle<Exception>(ex => ex is not OperationCanceledException and not UnroutableMessageException);

        return new ResiliencePipelineBuilder { TimeProvider = timeProvider ?? TimeProvider.System }
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = shouldHandle,
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = shouldHandle,
                FailureRatio = 0.5,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(10),
            })
            .Build();
    }
}
