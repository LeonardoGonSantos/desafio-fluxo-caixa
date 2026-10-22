using Lancamentos.External.Messaging;
using Lancamentos.External.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Shouldly;

namespace Lancamentos.External.Tests;

public class PublishResilienceTests
{
    private static ResiliencePipeline Pipeline(Action<PublishResilienceOptions>? configure = null)
    {
        var options = new PublishResilienceOptions
        {
            Timeout = TimeSpan.FromMilliseconds(150),
            BreakDuration = TimeSpan.FromMinutes(5),
            SamplingDuration = TimeSpan.FromMinutes(5),
            MinimumThroughput = 2,
            FailureRatio = 0.5,
        };
        configure?.Invoke(options);

        var builder = new ResiliencePipelineBuilder();
        PublishResilience.Configure(builder, options);
        return builder.Build();
    }

    [Fact]
    public async Task Sucesso_ExecutaOnceSemRetry()
    {
        var calls = 0;

        await Pipeline().ExecuteAsync(_ =>
        {
            calls++;
            return ValueTask.CompletedTask;
        });

        calls.ShouldBe(1);
    }

    [Fact]
    public async Task Falha_NaoRepeteInternamente_ORetryEDoOutbox()
    {
        var calls = 0;

        await Should.ThrowAsync<InvalidOperationException>(async () => await Pipeline().ExecuteAsync(
            _ =>
            {
                calls++;
                throw new InvalidOperationException("broker fora");
            }));

        calls.ShouldBe(1);
    }

    [Fact]
    public async Task FalhasDeInfraestrutura_AbremOCircuito_ENaoChamamMaisOBroker()
    {
        var pipeline = Pipeline();
        var calls = 0;

        for (var i = 0; i < 2; i++)
        {
            await Should.ThrowAsync<InvalidOperationException>(async () => await pipeline.ExecuteAsync(
                _ =>
                {
                    calls++;
                    throw new InvalidOperationException("broker fora");
                }));
        }

        await Should.ThrowAsync<BrokenCircuitException>(async () => await pipeline.ExecuteAsync(
            _ =>
            {
                calls++;
                return ValueTask.CompletedTask;
            }));

        calls.ShouldBe(2); // a terceira chamada foi barrada pelo circuito aberto
    }

    [Fact]
    public async Task MensagemSemRota_NaoAbreOCircuito()
    {
        var pipeline = Pipeline();

        for (var i = 0; i < 10; i++)
        {
            await Should.ThrowAsync<UnroutableMessageException>(async () => await pipeline.ExecuteAsync(
                _ => throw new UnroutableMessageException("lancamento.registrado.v1")));
        }

        // Circuito continua fechado: uma publicação saudável passa.
        var executou = false;
        await pipeline.ExecuteAsync(_ =>
        {
            executou = true;
            return ValueTask.CompletedTask;
        });
        executou.ShouldBeTrue();
    }

    [Fact]
    public async Task PublicacaoLenta_EstouraOTimeout()
    {
        await Should.ThrowAsync<TimeoutRejectedException>(async () => await Pipeline().ExecuteAsync(
            async ct => await Task.Delay(Timeout.Infinite, ct)));
    }

    [Theory]
    [InlineData(typeof(UnroutableMessageException), false)]
    [InlineData(typeof(OperationCanceledException), false)]
    [InlineData(typeof(InvalidOperationException), true)]
    [InlineData(typeof(TimeoutException), true)]
    public void IsInfrastructureFailure_ClassificaAsExcecoes(Type tipo, bool esperado)
    {
        var excecao = tipo == typeof(UnroutableMessageException)
            ? new UnroutableMessageException("rk")
            : (Exception)Activator.CreateInstance(tipo)!;

        PublishResilience.IsInfrastructureFailure(excecao).ShouldBe(esperado);
    }
}
