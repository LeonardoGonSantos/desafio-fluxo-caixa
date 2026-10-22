using Consolidacao.Application.SaldosDiarios.ObterRelatorioPeriodo;
using Consolidacao.Application.SaldosDiarios.ObterSaldoDiario;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Consolidacao.Application.Tests;

public class ValidatorTests
{
    // 2025-06-10 15:00 UTC = 12:00 em São Paulo
    private static readonly FakeTimeProvider Time = new(new DateTimeOffset(2025, 6, 10, 15, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly Hoje = new(2025, 6, 10);

    private readonly ObterSaldoDiarioQueryValidator _saldo = new(Time);
    private readonly ObterRelatorioPeriodoQueryValidator _relatorio = new(Time);

    [Fact]
    public void Saldo_DataFutura_Falha() =>
        _saldo.Validate(new ObterSaldoDiarioQuery(Hoje.AddDays(1))).IsValid.ShouldBeFalse();

    [Fact]
    public void Saldo_Hoje_Passa() =>
        _saldo.Validate(new ObterSaldoDiarioQuery(Hoje)).IsValid.ShouldBeTrue();

    [Fact]
    public void Saldo_UtcJaEhAmanhaMasSaoPauloNao_HojeLocalPassa()
    {
        var tarde = new FakeTimeProvider(new DateTimeOffset(2025, 6, 11, 1, 0, 0, TimeSpan.Zero)); // 22h do dia 10 em SP
        new ObterSaldoDiarioQueryValidator(tarde).Validate(new ObterSaldoDiarioQuery(Hoje)).IsValid.ShouldBeTrue();
        new ObterSaldoDiarioQueryValidator(tarde).Validate(new ObterSaldoDiarioQuery(Hoje.AddDays(1))).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Relatorio_DeMaiorQueAte_Falha() =>
        _relatorio.Validate(new ObterRelatorioPeriodoQuery(Hoje, Hoje.AddDays(-1))).IsValid.ShouldBeFalse();

    [Fact]
    public void Relatorio_MaisDe93Dias_Falha() =>
        _relatorio.Validate(new ObterRelatorioPeriodoQuery(Hoje.AddDays(-93), Hoje)).IsValid.ShouldBeFalse();

    [Fact]
    public void Relatorio_Exatamente93Dias_Passa() =>
        _relatorio.Validate(new ObterRelatorioPeriodoQuery(Hoje.AddDays(-92), Hoje)).IsValid.ShouldBeTrue();

    [Fact]
    public void Relatorio_AteFuturo_Falha() =>
        _relatorio.Validate(new ObterRelatorioPeriodoQuery(Hoje, Hoje.AddDays(1))).IsValid.ShouldBeFalse();
}
