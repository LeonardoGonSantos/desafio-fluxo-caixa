using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lancamentos.External.Messaging;
using RabbitMQ.Client;
using Shouldly;

namespace Lancamentos.IntegrationTests;

[Collection(InfraCollection.Name)]
public sealed class OutboxResilienceTests
{
    private readonly InfraFixture _infra;

    public OutboxResilienceTests(InfraFixture infra) => _infra = infra;

    private static async Task<Guid> PostLancamentoAsync(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos")
        {
            Content = JsonContent.Create(new
            {
                tipo = "Credito",
                valor = 55.5m,
                descricao = "Venda resiliente",
                dataCompetencia = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd"),
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task PostLancamento_RabbitMqForaDoAr_ContinuaRetornando201()
    {
        await using var factory = new LancamentosApiFactory(_infra, rabbitAvailable: false);
        using var client = factory.CreateClient();

        var id = await PostLancamentoAsync(client);

        (await factory.QueryScalarAsync<long>("SELECT count(*) FROM lancamentos WHERE id = @id", new { id })).ShouldBe(1);
        (await factory.QueryScalarAsync<long>(
            "SELECT count(*) FROM outbox_messages WHERE processed_at IS NULL AND payload->>'lancamentoId' = @id",
            new { id = id.ToString() })).ShouldBe(1);
    }

    [Fact]
    public async Task OutboxProcessor_BrokerFora_MantemMensagemPendenteERegistraTentativa()
    {
        await using var factory = new LancamentosApiFactory(_infra, rabbitAvailable: false);
        using var client = factory.CreateClient();
        var id = await PostLancamentoAsync(client);

        await Should.ThrowAsync<Exception>(() => factory.Outbox.ProcessBatchAsync(CancellationToken.None));

        var attempts = await factory.QueryScalarAsync<int>(
            "SELECT attempts FROM outbox_messages WHERE payload->>'lancamentoId' = @id", new { id = id.ToString() });
        attempts.ShouldBeGreaterThanOrEqualTo(1);
        (await factory.QueryScalarAsync<long>(
            "SELECT count(*) FROM outbox_messages WHERE processed_at IS NULL AND payload->>'lancamentoId' = @id",
            new { id = id.ToString() })).ShouldBe(1);
    }

    [Fact]
    public async Task OutboxProcessor_MensagemPendente_PublicaEMarcaProcessada()
    {
        await using var factory = new LancamentosApiFactory(_infra);
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        await BindQueueAsync(channel, factory.Exchange, "q.publica");
        var id = await PostLancamentoAsync(client);

        var published = await factory.Outbox.ProcessBatchAsync(CancellationToken.None);

        published.ShouldBe(1);
        var message = await channel.BasicGetAsync("q.publica", autoAck: true);
        message.ShouldNotBeNull();
        var payload = JsonDocument.Parse(message.Body.ToArray()).RootElement;
        payload.GetProperty("lancamentoId").GetGuid().ShouldBe(id);
        payload.GetProperty("tipo").GetString().ShouldBe("Credito");
        payload.GetProperty("valor").GetDecimal().ShouldBe(55.5m);
        message.BasicProperties.MessageId.ShouldBe(payload.GetProperty("eventId").GetGuid().ToString());
        message.BasicProperties.Type.ShouldBe("lancamento.registrado.v1");
        (await factory.QueryScalarAsync<long>(
            "SELECT count(*) FROM outbox_messages WHERE processed_at IS NOT NULL AND payload->>'lancamentoId' = @id",
            new { id = id.ToString() })).ShouldBe(1);
    }

    [Fact]
    public async Task OutboxProcessor_SemFilaVinculada_MantemPendenteEPublicaQuandoAFilaExistir()
    {
        await using var factory = new LancamentosApiFactory(_infra);
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        var id = await PostLancamentoAsync(client);

        // Consolidação ainda não declarou sua fila: mensagem sem rota NÃO pode ser perdida.
        await Should.ThrowAsync<UnroutableMessageException>(() => factory.Outbox.ProcessBatchAsync(CancellationToken.None));
        (await factory.QueryScalarAsync<long>(
            "SELECT count(*) FROM outbox_messages WHERE processed_at IS NULL AND payload->>'lancamentoId' = @id",
            new { id = id.ToString() })).ShouldBe(1);

        // Consolidação sobe e declara a fila: o acumulado é entregue.
        await BindQueueAsync(channel, factory.Exchange, "q.tardia");
        var published = await factory.Outbox.ProcessBatchAsync(CancellationToken.None);

        published.ShouldBe(1);
        (await channel.BasicGetAsync("q.tardia", autoAck: true)).ShouldNotBeNull();
    }

    [Fact]
    public async Task OutboxProcessor_ComLoopEmSegundoPlanoLigado_PublicaSemIntervencao()
    {
        await using var factory = new LancamentosApiFactory(_infra, backgroundOutbox: true);
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        await BindQueueAsync(channel, factory.Exchange, "q.background");

        var id = await PostLancamentoAsync(client);

        BasicGetResult? message = null;
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (message is null && DateTime.UtcNow < deadline)
        {
            message = await channel.BasicGetAsync("q.background", autoAck: true);
            await Task.Delay(100);
        }

        message.ShouldNotBeNull();
        JsonDocument.Parse(message.Body.ToArray()).RootElement.GetProperty("lancamentoId").GetGuid().ShouldBe(id);
        (await factory.QueryScalarAsync<long>(
            "SELECT count(*) FROM outbox_messages WHERE processed_at IS NOT NULL AND payload->>'lancamentoId' = @id",
            new { id = id.ToString() })).ShouldBe(1);
    }

    private static async Task BindQueueAsync(IChannel channel, string exchange, string queue)
    {
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(queue, exchange, "lancamento.registrado.v1");
    }
}
