using Consolidacao.Domain.SaldosDiarios;
using Shouldly;

namespace Consolidacao.Domain.Tests;

public class SaldoDiarioTests
{
    private static readonly DateOnly Dia = new(2025, 6, 10);
    private static readonly DateTimeOffset Agora = new(2025, 6, 10, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Abrir_NovoDia_TotaisZerados()
    {
        var saldo = SaldoDiario.Abrir(Dia, Agora);

        saldo.Data.ShouldBe(Dia);
        saldo.TotalCreditos.Amount.ShouldBe(0m);
        saldo.TotalDebitos.Amount.ShouldBe(0m);
        saldo.SaldoDoDia.ShouldBe(0m);
        saldo.AtualizadoEm.ShouldBe(Agora);
    }

    [Fact]
    public void Aplicar_Credito_SomaEmTotalCreditos()
    {
        var saldo = SaldoDiario.Abrir(Dia, Agora);

        var result = saldo.Aplicar(TipoMovimento.Credito, 100.25m, Agora);

        result.IsSuccess.ShouldBeTrue();
        saldo.TotalCreditos.Amount.ShouldBe(100.25m);
        saldo.TotalDebitos.Amount.ShouldBe(0m);
    }

    [Fact]
    public void Aplicar_Debito_SomaEmTotalDebitos()
    {
        var saldo = SaldoDiario.Abrir(Dia, Agora);

        saldo.Aplicar(TipoMovimento.Debito, 40m, Agora);

        saldo.TotalDebitos.Amount.ShouldBe(40m);
        saldo.TotalCreditos.Amount.ShouldBe(0m);
    }

    [Fact]
    public void SaldoDoDia_CreditosMenosDebitos()
    {
        var saldo = SaldoDiario.Abrir(Dia, Agora);
        saldo.Aplicar(TipoMovimento.Credito, 100m, Agora);
        saldo.Aplicar(TipoMovimento.Debito, 30m, Agora);

        saldo.SaldoDoDia.ShouldBe(70m);
    }

    [Fact]
    public void SaldoDoDia_DebitosMaioresQueCreditos_FicaNegativo()
    {
        var saldo = SaldoDiario.Abrir(Dia, Agora);
        saldo.Aplicar(TipoMovimento.Credito, 10m, Agora);
        saldo.Aplicar(TipoMovimento.Debito, 25m, Agora);

        saldo.SaldoDoDia.ShouldBe(-15m);
    }

    [Fact]
    public void Aplicar_AtualizaAtualizadoEm()
    {
        var saldo = SaldoDiario.Abrir(Dia, Agora);
        var depois = Agora.AddMinutes(5);

        saldo.Aplicar(TipoMovimento.Credito, 1m, depois);

        saldo.AtualizadoEm.ShouldBe(depois);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Aplicar_ValorZeroOuNegativo_RetornaErroENaoAltera(int valor)
    {
        var saldo = SaldoDiario.Abrir(Dia, Agora);

        var result = saldo.Aplicar(TipoMovimento.Credito, valor, Agora.AddHours(1));

        result.Error.ShouldBe(SaldoDiarioErrors.MovimentoInvalido);
        saldo.TotalCreditos.Amount.ShouldBe(0m);
        saldo.AtualizadoEm.ShouldBe(Agora);
    }

    [Fact]
    public void Aplicar_TipoInvalido_RetornaErro()
    {
        var saldo = SaldoDiario.Abrir(Dia, Agora);

        saldo.Aplicar((TipoMovimento)99, 10m, Agora).Error.ShouldBe(SaldoDiarioErrors.MovimentoInvalido);
    }

    [Fact]
    public void Aplicar_OrdemDiferenteDeMovimentos_MesmoResultado()
    {
        var movimentos = new (TipoMovimento Tipo, decimal Valor)[]
        {
            (TipoMovimento.Credito, 100m), (TipoMovimento.Debito, 30.5m), (TipoMovimento.Credito, 7.25m),
            (TipoMovimento.Debito, 12m), (TipoMovimento.Credito, 0.01m),
        };

        var direta = SaldoDiario.Abrir(Dia, Agora);
        foreach (var m in movimentos)
        {
            direta.Aplicar(m.Tipo, m.Valor, Agora);
        }

        var invertida = SaldoDiario.Abrir(Dia, Agora);
        foreach (var m in movimentos.Reverse())
        {
            invertida.Aplicar(m.Tipo, m.Valor, Agora);
        }

        invertida.TotalCreditos.ShouldBe(direta.TotalCreditos);
        invertida.TotalDebitos.ShouldBe(direta.TotalDebitos);
        invertida.SaldoDoDia.ShouldBe(direta.SaldoDoDia);
    }
}
