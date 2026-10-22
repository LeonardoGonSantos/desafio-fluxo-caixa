using Consolidacao.Application.Abstractions;
using Consolidacao.Application.SaldosDiarios;
using Consolidacao.Application.SaldosDiarios.ObterSaldoDiario;
using NSubstitute;
using Shouldly;

namespace Consolidacao.Application.Tests;

public class ObterSaldoDiarioQueryHandlerTests
{
    private static readonly DateOnly Dia = new(2025, 6, 10);
    private static readonly DateTimeOffset Atualizado = new(2025, 6, 10, 15, 0, 0, TimeSpan.Zero);

    private readonly ISaldoQueries _queries = Substitute.For<ISaldoQueries>();
    private readonly ISaldoCache _cache = Substitute.For<ISaldoCache>();
    private readonly ObterSaldoDiarioQueryHandler _handler;

    public ObterSaldoDiarioQueryHandlerTests()
    {
        _handler = new ObterSaldoDiarioQueryHandler(_queries, _cache);
        // Cache "miss": sempre executa a fábrica.
        _cache.GetOrCreateAsync(Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<SaldoDiarioResponse>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<SaldoDiarioResponse>>>()(CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DiaComMovimento_RetornaTotaisSaldoEAcumulado()
    {
        _queries.GetDiaAsync(Dia, Arg.Any<CancellationToken>()).Returns(new SaldoDiaRow(Dia, 300m, 120m, Atualizado));
        _queries.GetSaldoAcumuladoAteAsync(Dia, Arg.Any<CancellationToken>()).Returns(1000m);

        var result = await _handler.Handle(new ObterSaldoDiarioQuery(Dia), CancellationToken.None);

        result.Value.ShouldBe(new SaldoDiarioResponse(Dia, 300m, 120m, 180m, 1000m, Atualizado));
    }

    [Fact]
    public async Task Handle_DiaSemMovimento_RetornaZerosComAcumuladoAnterior()
    {
        _queries.GetDiaAsync(Dia, Arg.Any<CancellationToken>()).Returns((SaldoDiaRow?)null);
        _queries.GetSaldoAcumuladoAteAsync(Dia, Arg.Any<CancellationToken>()).Returns(500m);

        var result = await _handler.Handle(new ObterSaldoDiarioQuery(Dia), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new SaldoDiarioResponse(Dia, 0m, 0m, 0m, 500m, null));
    }

    [Fact]
    public async Task Handle_UsaChaveDeCachePorData()
    {
        _queries.GetDiaAsync(default, default).ReturnsForAnyArgs((SaldoDiaRow?)null);

        await _handler.Handle(new ObterSaldoDiarioQuery(Dia), CancellationToken.None);

        await _cache.Received(1).GetOrCreateAsync(
            "saldo:2025-06-10",
            Arg.Any<Func<CancellationToken, Task<SaldoDiarioResponse>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CacheHit_NaoConsultaOBanco()
    {
        var emCache = new SaldoDiarioResponse(Dia, 1m, 0m, 1m, 1m, Atualizado);
        _cache.GetOrCreateAsync(Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<SaldoDiarioResponse>>>(), Arg.Any<CancellationToken>())
            .Returns(emCache);

        var result = await _handler.Handle(new ObterSaldoDiarioQuery(Dia), CancellationToken.None);

        result.Value.ShouldBe(emCache);
        await _queries.DidNotReceiveWithAnyArgs().GetDiaAsync(default, default);
    }
}
