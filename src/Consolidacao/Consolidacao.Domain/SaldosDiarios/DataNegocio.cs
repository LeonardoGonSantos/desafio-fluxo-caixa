namespace Consolidacao.Domain.SaldosDiarios;

/// <summary>Regra de "dia" do negócio: fuso America/Sao_Paulo.</summary>
public static class DataNegocio
{
    private static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateOnly Hoje(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Fuso).DateTime);
}
