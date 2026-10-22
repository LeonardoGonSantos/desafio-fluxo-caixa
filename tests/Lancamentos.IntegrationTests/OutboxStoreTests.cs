using Dapper;
using Lancamentos.External.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace Lancamentos.IntegrationTests;

[Collection(InfraCollection.Name)]
public sealed class OutboxStoreTests
{
    private readonly InfraFixture _infra;

    public OutboxStoreTests(InfraFixture infra) => _infra = infra;

    private static async Task<List<Guid>> SeedAsync(LancamentosApiFactory factory, int count)
    {
        var ids = new List<Guid>();
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            await connection.ExecuteAsync(
                """
                INSERT INTO outbox_messages (id, type, routing_key, payload, occurred_at)
                VALUES (@id, 't', 'rk', '{}'::jsonb, now() + (@i * interval '1 millisecond'))
                """,
                new { id, i });
        }

        return ids;
    }

    private static IOutboxStore StoreOf(LancamentosApiFactory factory) =>
        factory.Services.GetRequiredService<IOutboxStore>();

    private static Task<int> CountLocked(LancamentosApiFactory factory) =>
        factory.QueryScalarAsync<int>("SELECT count(*)::int FROM outbox_messages WHERE locked_until IS NOT NULL");

    [Fact]
    public async Task ClaimBatch_DuasInstanciasConcorrentes_NuncaRecebemAMesmaMensagem()
    {
        await using var factory = new LancamentosApiFactory(_infra);
        using var _ = factory.CreateClient(); // sobe o host (migrations)
        var seeded = await SeedAsync(factory, 40);
        var store = StoreOf(factory);

        var claims = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => store.ClaimBatchAsync(15, TimeSpan.FromSeconds(30), CancellationToken.None)));

        var all = claims.SelectMany(c => c.Select(m => m.Id)).ToList();
        all.Count.ShouldBe(all.Distinct().Count());     // nenhuma duplicada entre as reservas
        all.Count.ShouldBe(40);                         // e todas foram reservadas
        all.ToHashSet().SetEquals(seeded).ShouldBeTrue();
    }

    [Fact]
    public async Task ClaimBatch_RetornaEmOrdemDeOcorrenciaERespeitaOTamanhoDoLote()
    {
        await using var factory = new LancamentosApiFactory(_infra);
        using var _ = factory.CreateClient();
        var seeded = await SeedAsync(factory, 10);

        var batch = await StoreOf(factory).ClaimBatchAsync(4, TimeSpan.FromSeconds(30), CancellationToken.None);

        batch.Select(m => m.Id).ShouldBe(seeded.Take(4));
    }

    [Fact]
    public async Task ClaimBatch_MensagemComLeaseVigente_NaoEReentregue()
    {
        await using var factory = new LancamentosApiFactory(_infra);
        using var _ = factory.CreateClient();
        await SeedAsync(factory, 3);
        var store = StoreOf(factory);

        (await store.ClaimBatchAsync(10, TimeSpan.FromSeconds(30), CancellationToken.None)).Count.ShouldBe(3);
        (await store.ClaimBatchAsync(10, TimeSpan.FromSeconds(30), CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ClaimBatch_LeaseExpirado_MensagemVoltaASerReivindicada()
    {
        await using var factory = new LancamentosApiFactory(_infra);
        using var _ = factory.CreateClient();
        await SeedAsync(factory, 2);
        var store = StoreOf(factory);

        (await store.ClaimBatchAsync(10, TimeSpan.FromSeconds(1), CancellationToken.None)).Count.ShouldBe(2);
        await Task.Delay(TimeSpan.FromMilliseconds(1300)); // "processo morreu": o lease vence

        (await store.ClaimBatchAsync(10, TimeSpan.FromSeconds(30), CancellationToken.None)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task MarkProcessed_RemoveDaFilaDePendentesELimpaOLease()
    {
        await using var factory = new LancamentosApiFactory(_infra);
        using var _ = factory.CreateClient();
        await SeedAsync(factory, 1);
        var store = StoreOf(factory);
        var message = (await store.ClaimBatchAsync(10, TimeSpan.FromSeconds(30), CancellationToken.None)).Single();

        await store.MarkProcessedAsync(message.Id, CancellationToken.None);

        (await factory.QueryScalarAsync<int>("SELECT count(*)::int FROM outbox_messages WHERE processed_at IS NOT NULL")).ShouldBe(1);
        (await CountLocked(factory)).ShouldBe(0);
        (await store.ClaimBatchAsync(10, TimeSpan.FromSeconds(30), CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task MarkFailed_RegistraTentativaEErro_ELiberaParaNovaTentativa()
    {
        await using var factory = new LancamentosApiFactory(_infra);
        using var _ = factory.CreateClient();
        await SeedAsync(factory, 1);
        var store = StoreOf(factory);
        var message = (await store.ClaimBatchAsync(10, TimeSpan.FromSeconds(30), CancellationToken.None)).Single();

        await store.MarkFailedAsync(message.Id, "broker fora", CancellationToken.None);

        (await factory.QueryScalarAsync<int>("SELECT attempts FROM outbox_messages")).ShouldBe(1);
        (await factory.QueryScalarAsync<string>("SELECT last_error FROM outbox_messages")).ShouldBe("broker fora");
        (await store.ClaimBatchAsync(10, TimeSpan.FromSeconds(30), CancellationToken.None)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Release_LiberaSemRegistrarTentativa()
    {
        await using var factory = new LancamentosApiFactory(_infra);
        using var _ = factory.CreateClient();
        await SeedAsync(factory, 2);
        var store = StoreOf(factory);
        var batch = await store.ClaimBatchAsync(10, TimeSpan.FromSeconds(30), CancellationToken.None);

        await store.ReleaseAsync(batch.Select(m => m.Id).ToList(), CancellationToken.None);

        (await factory.QueryScalarAsync<int>("SELECT sum(attempts)::int FROM outbox_messages")).ShouldBe(0);
        (await store.ClaimBatchAsync(10, TimeSpan.FromSeconds(30), CancellationToken.None)).Count.ShouldBe(2);
    }
}
