using System.Net.Http.Json;
using System.Text.Json;
using Consolidacao.Domain.SaldosDiarios;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Consolidacao.IntegrationTests;

[Collection(InfraCollection.Name)]
public sealed class ConsumerResilienceTests
{
    private const string Dia = "2025-02-20";
    private readonly InfraFixture _infra;

    public ConsumerResilienceTests(InfraFixture infra) => _infra = infra;

    /// <summary>Repositório que sempre falha: simula o banco indisponível para o consumidor.</summary>
    private sealed class RepositorioIndisponivel : ISaldoDiarioRepository
    {
        public Task<SaldoDiario> GetForUpdateOrCreateAsync(DateOnly data, DateTimeOffset agora, CancellationToken cancellationToken) =>
            throw new TimeoutException("banco indisponível (simulado)");

        public Task SaveAsync(SaldoDiario saldo, CancellationToken cancellationToken) =>
            throw new TimeoutException("banco indisponível (simulado)");
    }

    [Fact]
    public async Task Consumer_FalhaTransitoriaRepetida_AposEsgotarAsTentativasVaiParaDlq_SemAlterarSaldo()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra, overrideServices: services =>
        {
            services.AddScoped<ISaldoDiarioRepository, RepositorioIndisponivel>();
        });
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        await factory.WaitForConsumerAsync();

        await factory.PublishAsync(channel, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Credito", 77m, Dia));

        // MaxProcessingAttempts = 3: a mensagem é reprocessada com contador de tentativas e então vai para a DLQ.
        RabbitMQ.Client.BasicGetResult? naDlq = null;
        await Eventually.ForAsync(async () =>
        {
            naDlq = await channel.BasicGetAsync(factory.DeadLetterQueue, autoAck: true);
            return naDlq is not null;
        }, timeoutSeconds: 60);

        naDlq.ShouldNotBeNull();
        (await client.GetFromJsonAsync<JsonElement>($"/api/v1/saldos-diarios/{Dia}"))
            .GetProperty("totalCreditos").GetDecimal().ShouldBe(0m);
        // A transação foi revertida: o evento NÃO ficou marcado como processado (poderia ser reprocessado depois).
        (await factory.QueryScalarAsync<long>("SELECT count(*) FROM processed_events")).ShouldBe(0);
    }

    [Fact]
    public async Task Consumer_QuedaDasConexoesNoBroker_ReconectaSozinhoEContinuaConsumindo()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();
        await factory.WaitForConsumerAsync();
        await using (var antes = await factory.CreateRabbitChannelAsync())
        {
            await factory.PublishAsync(antes, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Credito", 10m, Dia));
        }

        await Eventually.ForAsync(async () => await TotalCreditosAsync(client) == 10m);

        await factory.CloseAllBrokerConnectionsAsync();

        // Depois da queda, uma mensagem nova precisa ser consumida: o consumo foi restabelecido.
        await Eventually.ForAsync(async () =>
        {
            try
            {
                await using var depois = await factory.CreateRabbitChannelAsync();
                await factory.PublishAsync(depois, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Credito", 5m, Dia));
                return true;
            }
            catch (Exception)
            {
                return false; // broker ainda reabrindo conexões
            }
        });

        await Eventually.ForAsync(async () => await TotalCreditosAsync(client) >= 15m, timeoutSeconds: 60);
    }

    private static async Task<decimal> TotalCreditosAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/v1/saldos-diarios/{Dia}")).GetProperty("totalCreditos").GetDecimal();
}
