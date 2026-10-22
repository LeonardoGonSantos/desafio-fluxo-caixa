using Dapper;
using Lancamentos.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace Lancamentos.IntegrationTests;

[Collection(InfraCollection.Name)]
public sealed class IdempotencyStoreTests
{
    private readonly InfraFixture _infra;

    public IdempotencyStoreTests(InfraFixture infra) => _infra = infra;

    private static async Task<(LancamentosApiFactory Factory, IIdempotencyStore Store)> StartAsync(InfraFixture infra)
    {
        var factory = new LancamentosApiFactory(infra);
        using var _ = factory.CreateClient(); // sobe o host (migrations)
        return (factory, factory.Services.GetRequiredService<IIdempotencyStore>());
    }

    [Fact]
    public async Task Reserve_ChaveNova_Acquired_ESegundaReservaIgualFicaEmAndamento()
    {
        var (factory, store) = await StartAsync(_infra);
        await using var _ = factory;

        (await store.ReserveAsync("k1", "hash-a", CancellationToken.None)).Outcome.ShouldBe(ReservationOutcome.Acquired);
        (await store.ReserveAsync("k1", "hash-a", CancellationToken.None)).Outcome.ShouldBe(ReservationOutcome.InProgress);
    }

    [Fact]
    public async Task Reserve_MesmaChaveComOutroCorpo_HashMismatch()
    {
        var (factory, store) = await StartAsync(_infra);
        await using var _ = factory;
        await store.ReserveAsync("k2", "hash-a", CancellationToken.None);

        (await store.ReserveAsync("k2", "hash-b", CancellationToken.None)).Outcome.ShouldBe(ReservationOutcome.HashMismatch);
    }

    [Fact]
    public async Task Complete_DepoisReservaIgual_DevolveARespostaGuardada()
    {
        var (factory, store) = await StartAsync(_infra);
        await using var _ = factory;
        await store.ReserveAsync("k3", "hash-a", CancellationToken.None);
        await store.CompleteAsync("k3", new IdempotentResponse(201, "{\"id\":1}", "/api/v1/lancamentos/1"), CancellationToken.None);

        var again = await store.ReserveAsync("k3", "hash-a", CancellationToken.None);

        again.Outcome.ShouldBe(ReservationOutcome.Completed);
        again.Response.ShouldBe(new IdempotentResponse(201, "{\"id\":1}", "/api/v1/lancamentos/1"));
    }

    [Fact]
    public async Task Release_PermiteNovaTentativaComAMesmaChave()
    {
        var (factory, store) = await StartAsync(_infra);
        await using var _ = factory;
        await store.ReserveAsync("k4", "hash-a", CancellationToken.None);

        await store.ReleaseAsync("k4", CancellationToken.None);

        (await store.ReserveAsync("k4", "hash-a", CancellationToken.None)).Outcome.ShouldBe(ReservationOutcome.Acquired);
    }

    [Fact]
    public async Task Release_NaoApagaRespostaJaConcluida()
    {
        var (factory, store) = await StartAsync(_infra);
        await using var _ = factory;
        await store.ReserveAsync("k5", "hash-a", CancellationToken.None);
        await store.CompleteAsync("k5", new IdempotentResponse(201, "{}", null), CancellationToken.None);

        await store.ReleaseAsync("k5", CancellationToken.None);

        (await store.ReserveAsync("k5", "hash-a", CancellationToken.None)).Outcome.ShouldBe(ReservationOutcome.Completed);
    }

    [Fact]
    public async Task Reserve_ReservaAbandonada_PodeSerReivindicadaDepoisDoPrazo()
    {
        var (factory, store) = await StartAsync(_infra);
        await using var _ = factory;
        await store.ReserveAsync("k6", "hash-a", CancellationToken.None);

        // "O processo morreu": envelhece a reserva além do prazo de abandono (60 s).
        await using (var connection = new NpgsqlConnection(factory.ConnectionString))
        {
            await connection.ExecuteAsync("UPDATE idempotency_keys SET created_at = now() - interval '5 minutes' WHERE key = 'k6'");
        }

        (await store.ReserveAsync("k6", "hash-a", CancellationToken.None)).Outcome.ShouldBe(ReservationOutcome.Acquired);
    }

    [Fact]
    public async Task Reserve_ConcorrenciaNaMesmaChave_ExatamenteUmAcquired()
    {
        var (factory, store) = await StartAsync(_infra);
        await using var _ = factory;

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => store.ReserveAsync("k7", "hash-a", CancellationToken.None)));

        results.Count(r => r.Outcome == ReservationOutcome.Acquired).ShouldBe(1);
        results.Count(r => r.Outcome == ReservationOutcome.InProgress).ShouldBe(19);
    }
}
