using FluxoCaixa.BuildingBlocks.Domain;
using Shouldly;

namespace FluxoCaixa.BuildingBlocks.Tests;

public class MoneyTests
{
    [Fact]
    public void Criar_ValorNegativo_Falha()
    {
        var result = Money.Create(-0.01m);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Money.Negative);
    }

    [Theory]
    [InlineData(10.001)]
    [InlineData(0.005)]
    public void Criar_MaisDeDuasCasasDecimais_Falha(double valor)
    {
        var result = Money.Create((decimal)valor);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Money.TooManyDecimals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10.5)]
    [InlineData(99999999.99)]
    public void Criar_ValorValido_Sucesso(double valor)
    {
        var result = Money.Create((decimal)valor);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Amount.ShouldBe((decimal)valor);
    }

    [Fact]
    public void Somar_DoisValores_RetornaSoma()
    {
        var soma = Money.FromTrusted(10.10m) + Money.FromTrusted(0.20m);

        soma.Amount.ShouldBe(10.30m);
    }

    [Fact]
    public void Igualdade_MesmoValor_SaoIguais()
    {
        Money.FromTrusted(5m).ShouldBe(Money.FromTrusted(5.00m));
        (Money.FromTrusted(5m) == Money.FromTrusted(5m)).ShouldBeTrue();
    }
}
