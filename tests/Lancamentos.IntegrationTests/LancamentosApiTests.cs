using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Lancamentos.Application.Lancamentos;
using Shouldly;

namespace Lancamentos.IntegrationTests;

[Collection(InfraCollection.Name)]
public sealed class LancamentosApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly LancamentosApiFactory _factory;
    private readonly HttpClient _client;

    public LancamentosApiTests(InfraFixture infra)
    {
        _factory = new LancamentosApiFactory(infra);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static object Corpo(string tipo = "Credito", decimal valor = 100.50m, string descricao = "Venda balcão") => new
    {
        tipo,
        valor,
        descricao,
        dataCompetencia = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd"),
    };

    private Task<HttpResponseMessage> Post(object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lancamentos") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return _client.SendAsync(request);
    }

    [Fact]
    public async Task Migrations_BancoVazio_CriamTodasAsTabelas()
    {
        // O primeiro request força o host a subir (e a rodar as migrations).
        (await _client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var tabelas = await _factory.QueryScalarAsync<long>("""
            SELECT count(*) FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name IN ('lancamentos', 'outbox_messages', 'idempotency_keys')
            """);

        tabelas.ShouldBe(3);
    }

    [Fact]
    public async Task PostLancamento_Valido_Retorna201ComLocation()
    {
        var response = await Post(Corpo());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = (await response.Content.ReadFromJsonAsync<LancamentoResponse>(Json))!;
        body.Tipo.ShouldBe("Credito");
        body.Valor.ShouldBe(100.50m);
        response.Headers.Location!.ToString().ShouldEndWith($"/api/v1/lancamentos/{body.Id}");

        var get = await _client.GetAsync(response.Headers.Location);
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostLancamento_Valido_GravaLancamentoEOutboxNaMesmaTransacao()
    {
        var response = await Post(Corpo());
        var body = (await response.Content.ReadFromJsonAsync<LancamentoResponse>(Json))!;

        (await _factory.QueryScalarAsync<long>("SELECT count(*) FROM lancamentos WHERE id = @id", new { id = body.Id }))
            .ShouldBe(1);
        (await _factory.QueryScalarAsync<long>(
            "SELECT count(*) FROM outbox_messages WHERE payload->>'lancamentoId' = @id AND processed_at IS NULL",
            new { id = body.Id.ToString() })).ShouldBe(1);
    }

    [Fact]
    public async Task PostLancamento_SemIdempotencyKey_Retorna400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/lancamentos", Corpo());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostLancamento_MesmaIdempotencyKey_RetornaMesmaRespostaSemDuplicar()
    {
        var key = Guid.NewGuid().ToString();
        var descricao = $"Venda {Guid.NewGuid()}";

        var first = await Post(Corpo(descricao: descricao), key);
        var second = await Post(Corpo(descricao: descricao), key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.Headers.GetValues("Idempotent-Replayed").ShouldContain("true");
        var a = (await first.Content.ReadFromJsonAsync<LancamentoResponse>(Json))!;
        var b = (await second.Content.ReadFromJsonAsync<LancamentoResponse>(Json))!;
        b.Id.ShouldBe(a.Id);
        (await _factory.QueryScalarAsync<long>(
            "SELECT count(*) FROM lancamentos WHERE descricao = @descricao", new { descricao })).ShouldBe(1);
    }

    [Fact]
    public async Task PostLancamento_MesmaKeyComCorpoDiferente_Retorna422()
    {
        var key = Guid.NewGuid().ToString();
        await Post(Corpo(valor: 10m), key);

        var response = await Post(Corpo(valor: 20m), key);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PostLancamento_RequisicoesConcorrentesComAMesmaKey_CriamUmUnicoLancamento()
    {
        var key = Guid.NewGuid().ToString();
        var descricao = $"Concorrente {Guid.NewGuid()}";

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Post(Corpo(descricao: descricao), key)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBeGreaterThanOrEqualTo(1);
        responses.All(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict).ShouldBeTrue();
        (await _factory.QueryScalarAsync<long>(
            "SELECT count(*) FROM lancamentos WHERE descricao = @descricao", new { descricao })).ShouldBe(1);
    }

    [Fact]
    public async Task PostLancamento_Invalido_Retorna400ProblemDetails()
    {
        var response = await Post(Corpo(valor: 0m));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task PostLancamento_DataFutura_Retorna400ComCodigoDeDominio()
    {
        var body = new { tipo = "Credito", valor = 10m, descricao = "Venda futura", dataCompetencia = "2999-01-01" };

        var response = await Post(body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe("lancamento.data_futura");
    }

    [Fact]
    public async Task PostEstorno_Valido_Retorna201EMarcaOriginalComoEstornado()
    {
        var original = (await (await Post(Corpo(valor: 70m))).Content.ReadFromJsonAsync<LancamentoResponse>(Json))!;

        var response = await _client.PostAsJsonAsync($"/api/v1/lancamentos/{original.Id}/estornos", new { motivo = "Engano" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var estorno = (await response.Content.ReadFromJsonAsync<LancamentoResponse>(Json))!;
        estorno.Tipo.ShouldBe("Debito");
        estorno.LancamentoOriginalId.ShouldBe(original.Id);
        var atualizado = (await _client.GetFromJsonAsync<LancamentoResponse>($"/api/v1/lancamentos/{original.Id}", Json))!;
        atualizado.Status.ShouldBe("Estornado");
    }

    [Fact]
    public async Task PostEstorno_Duplicado_Retorna409()
    {
        var original = (await (await Post(Corpo())).Content.ReadFromJsonAsync<LancamentoResponse>(Json))!;
        await _client.PostAsJsonAsync($"/api/v1/lancamentos/{original.Id}/estornos", new { motivo = "Primeiro" });

        var response = await _client.PostAsJsonAsync($"/api/v1/lancamentos/{original.Id}/estornos", new { motivo = "Segundo" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostEstorno_Concorrente_GeraUmUnicoEstorno()
    {
        var original = (await (await Post(Corpo())).Content.ReadFromJsonAsync<LancamentoResponse>(Json))!;

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            _client.PostAsJsonAsync($"/api/v1/lancamentos/{original.Id}/estornos", new { motivo = $"Tentativa {i}" })));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(5);
    }

    [Fact]
    public async Task PostEstorno_LancamentoInexistente_Retorna404()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/lancamentos/{Guid.NewGuid()}/estornos", new { motivo = "Engano" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLancamento_Inexistente_Retorna404()
    {
        (await _client.GetAsync($"/api/v1/lancamentos/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLancamentos_PorPeriodo_RetornaPaginado()
    {
        for (var i = 0; i < 3; i++)
        {
            await Post(Corpo(descricao: $"Item de lista {i}"));
        }

        var dia = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd");
        var page = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/lancamentos?de={dia}&ate={dia}&page=1&pageSize=2");

        page.GetProperty("items").GetArrayLength().ShouldBe(2);
        page.GetProperty("totalItems").GetInt64().ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetLancamentos_SemPeriodo_Retorna400()
    {
        (await _client.GetAsync("/api/v1/lancamentos")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Swagger_Habilitado_ExpoeDocumentoOpenApi()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Idempotency-Key");
    }
}
