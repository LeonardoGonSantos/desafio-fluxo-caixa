using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Consolidacao.IntegrationTests;

[Collection(InfraCollection.Name)]
public sealed class ConsolidacaoApiTests
{
    private const string Dia = "2025-01-15";
    private readonly InfraFixture _infra;

    public ConsolidacaoApiTests(InfraFixture infra) => _infra = infra;

    private static async Task<JsonElement> GetSaldoAsync(HttpClient client, string data) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/v1/saldos-diarios/{data}");

    private static Task AguardarSaldoAsync(HttpClient client, string data, decimal creditos, decimal debitos) =>
        Eventually.ForAsync(async () =>
        {
            var saldo = await GetSaldoAsync(client, data);
            return saldo.GetProperty("totalCreditos").GetDecimal() == creditos
                && saldo.GetProperty("totalDebitos").GetDecimal() == debitos;
        });

    [Fact]
    public async Task Migrations_BancoVazio_CriamTodasAsTabelas()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();
        (await client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var tabelas = await factory.QueryScalarAsync<long>("""
            SELECT count(*) FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name IN ('saldo_diario', 'processed_events')
            """);

        tabelas.ShouldBe(2);
    }

    [Fact]
    public async Task GetSaldoDiario_DiaSemMovimento_Retorna200ComZeros()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();

        var saldo = await GetSaldoAsync(client, Dia);

        saldo.GetProperty("totalCreditos").GetDecimal().ShouldBe(0m);
        saldo.GetProperty("saldoDoDia").GetDecimal().ShouldBe(0m);
        saldo.GetProperty("saldoAcumulado").GetDecimal().ShouldBe(0m);
    }

    [Fact]
    public async Task GetSaldoDiario_DataFutura_Retorna400()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/saldos-diarios/2999-01-01");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task GetSaldoDiario_DataMalformada_Retorna400()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/v1/saldos-diarios/ontem")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Consumer_MensagemValida_AtualizaSaldoDoDia()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        await factory.WaitForConsumerAsync();

        await factory.PublishAsync(channel, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Credito", 150.75m, Dia));
        await factory.PublishAsync(channel, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Debito", 50.25m, Dia));

        await AguardarSaldoAsync(client, Dia, 150.75m, 50.25m);
        var saldo = await GetSaldoAsync(client, Dia);
        saldo.GetProperty("saldoDoDia").GetDecimal().ShouldBe(100.50m);
        saldo.GetProperty("saldoAcumulado").GetDecimal().ShouldBe(100.50m);
    }

    [Fact]
    public async Task Consumer_MensagemDuplicada_AplicaUmaUnicaVez()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        await factory.WaitForConsumerAsync();
        var eventId = Guid.NewGuid();

        for (var i = 0; i < 3; i++)
        {
            await factory.PublishAsync(channel, ConsolidacaoApiFactory.Evento(eventId, "Credito", 100m, Dia));
        }

        // Evento sentinela publicado depois: quando aparece, as duplicatas anteriores já foram processadas.
        await factory.PublishAsync(channel, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Credito", 1m, Dia));
        await AguardarSaldoAsync(client, Dia, 101m, 0m);
        (await factory.QueryScalarAsync<long>("SELECT count(*) FROM processed_events WHERE event_id = @eventId", new { eventId }))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Consumer_100MensagensConcorrentesNoMesmoDia_SomaExata()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        await factory.WaitForConsumerAsync();

        for (var i = 0; i < 100; i++)
        {
            var tipo = i % 2 == 0 ? "Credito" : "Debito";
            await factory.PublishAsync(channel, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), tipo, 10.10m, Dia));
        }

        await AguardarSaldoAsync(client, Dia, 505.00m, 505.00m);
    }

    [Fact]
    public async Task Consumer_MensagemMalformada_VaiParaDlq()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        await factory.WaitForConsumerAsync();

        await factory.PublishAsync(channel, "isto não é json");

        await EsperarNaDlqAsync(channel, factory);
    }

    [Fact]
    public async Task Consumer_TipoDeMensagemDesconhecido_VaiParaDlqSemAlterarSaldo()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        await factory.WaitForConsumerAsync();

        await factory.PublishAsync(
            channel, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Credito", 99m, Dia), type: "outro.evento.v9");

        await EsperarNaDlqAsync(channel, factory);
        (await GetSaldoAsync(client, Dia)).GetProperty("totalCreditos").GetDecimal().ShouldBe(0m);
    }

    [Fact]
    public async Task Consumer_TipoDeMovimentoInvalido_VaiParaDlq()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        await factory.WaitForConsumerAsync();

        await factory.PublishAsync(channel, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Transferencia", 10m, Dia));

        await EsperarNaDlqAsync(channel, factory);
    }

    [Fact]
    public async Task GetSaldoDiario_LancamentoRetroativo_AtualizaAcumuladoDosDiasSeguintes()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        await factory.WaitForConsumerAsync();

        await factory.PublishAsync(channel, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Credito", 100m, "2025-01-16"));
        await AguardarSaldoAsync(client, "2025-01-16", 100m, 0m);
        (await GetSaldoAsync(client, "2025-01-16")).GetProperty("saldoAcumulado").GetDecimal().ShouldBe(100m);

        // Chega depois, mas com data de competência anterior.
        await factory.PublishAsync(channel, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Credito", 40m, "2025-01-14"));

        await Eventually.ForAsync(async () =>
            (await GetSaldoAsync(client, "2025-01-16")).GetProperty("saldoAcumulado").GetDecimal() == 140m);
    }

    [Fact]
    public async Task GetRelatorioPeriodo_RetornaUmItemPorDiaComAcumulado()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();
        await using var channel = await factory.CreateRabbitChannelAsync();
        await factory.WaitForConsumerAsync();
        await factory.PublishAsync(channel, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Credito", 200m, "2025-01-10"));
        await factory.PublishAsync(channel, ConsolidacaoApiFactory.Evento(Guid.NewGuid(), "Debito", 50m, "2025-01-12"));
        await AguardarSaldoAsync(client, "2025-01-12", 0m, 50m);
        await AguardarSaldoAsync(client, "2025-01-10", 200m, 0m);

        var relatorio = await client.GetFromJsonAsync<JsonElement>("/api/v1/saldos-diarios?de=2025-01-10&ate=2025-01-12");

        var dias = relatorio.GetProperty("dias");
        dias.GetArrayLength().ShouldBe(3);
        dias[0].GetProperty("saldoAcumulado").GetDecimal().ShouldBe(200m);
        dias[1].GetProperty("saldoAcumulado").GetDecimal().ShouldBe(200m);
        dias[2].GetProperty("saldoAcumulado").GetDecimal().ShouldBe(150m);
        relatorio.GetProperty("saldoDoPeriodo").GetDecimal().ShouldBe(150m);
    }

    [Fact]
    public async Task GetRelatorioPeriodo_SemParametros_Retorna400()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/v1/saldos-diarios")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetSaldoDiario_AcimaDoLimite_Retorna429ComRetryAfter()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra, tokenLimit: 5);
        using var client = factory.CreateClient();

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 30; i++)
        {
            responses.Add(await client.GetAsync($"/api/v1/saldos-diarios/{Dia}"));
        }

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBeGreaterThanOrEqualTo(5);
        var limitada = responses.First(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        limitada.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task Swagger_Habilitado_ExpoeDocumentoOpenApi()
    {
        await using var factory = new ConsolidacaoApiFactory(_infra);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("/api/v1/saldos-diarios/{data}");
    }

    private static async Task EsperarNaDlqAsync(RabbitMQ.Client.IChannel channel, ConsolidacaoApiFactory factory)
    {
        RabbitMQ.Client.BasicGetResult? mensagem = null;
        await Eventually.ForAsync(async () =>
        {
            mensagem = await channel.BasicGetAsync(factory.DeadLetterQueue, autoAck: true);
            return mensagem is not null;
        });
        mensagem.ShouldNotBeNull();
    }
}
