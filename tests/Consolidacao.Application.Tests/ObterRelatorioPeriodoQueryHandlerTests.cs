using Consolidacao.Application.Abstractions;
using Consolidacao.Application.SaldosDiarios.ObterRelatorioPeriodo;
using NSubstitute;
using Shouldly;

namespace Consolidacao.Application.Tests;

public class ObterRelatorioPeriodoQueryHandlerTests
{
    private static readonly DateTimeOffset Atualizado = new(2025, 6, 10, 15, 0, 0, TimeSpan.Zero);
    private readonly ISaldoQueries _queries = Substitute.For<ISaldoQueries>();

    [Fact]
    public async Task Handle_PreencheDiasSemMovimentoEPropagaOAcumulado()
    {
        var de = new DateOnly(2025, 6, 10);
        var ate = new DateOnly(2025, 6, 13);
        _queries.GetSaldoAcumuladoAteAsync(de.AddDays(-1), Arg.Any<CancellationToken>()).Returns(1000m);
        _queries.GetPeriodoAsync(de, ate, Arg.Any<CancellationToken>()).Returns(new[]
        {
            new SaldoDiaRow(new DateOnly(2025, 6, 10), 200m, 50m, Atualizado),
            new SaldoDiaRow(new DateOnly(2025, 6, 13), 0m, 400m, Atualizado),
        });
        var handler = new ObterRelatorioPeriodoQueryHandler(_queries);

        var result = (await handler.Handle(new ObterRelatorioPeriodoQuery(de, ate), CancellationToken.None)).Value;

        result.Dias.Count.ShouldBe(4);
        result.Dias.Select(d => d.SaldoDoDia).ShouldBe([150m, 0m, 0m, -400m]);
        result.Dias.Select(d => d.SaldoAcumulado).ShouldBe([1150m, 1150m, 1150m, 750m]);
        result.Dias[1].AtualizadoEm.ShouldBeNull();
        result.TotalCreditos.ShouldBe(200m);
        result.TotalDebitos.ShouldBe(450m);
        result.SaldoDoPeriodo.ShouldBe(-250m);
    }

    [Fact]
    public async Task Handle_PeriodoSemNenhumMovimento_RetornaZerosComAcumuladoInicial()
    {
        var dia = new DateOnly(2025, 6, 10);
        _queries.GetSaldoAcumuladoAteAsync(dia.AddDays(-1), Arg.Any<CancellationToken>()).Returns(75m);
        _queries.GetPeriodoAsync(dia, dia, Arg.Any<CancellationToken>()).Returns(Array.Empty<SaldoDiaRow>());
        var handler = new ObterRelatorioPeriodoQueryHandler(_queries);

        var result = (await handler.Handle(new ObterRelatorioPeriodoQuery(dia, dia), CancellationToken.None)).Value;

        result.Dias.ShouldHaveSingleItem().SaldoAcumulado.ShouldBe(75m);
    }
}
