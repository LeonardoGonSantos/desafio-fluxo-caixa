using FluxoCaixa.BuildingBlocks.Domain;
using FluxoCaixa.BuildingBlocks.Results;

namespace Consolidacao.Domain.SaldosDiarios;

/// <summary>
/// Aggregate Root: totais de créditos e débitos de um dia de competência.
/// A identidade é a própria data. Somar é comutativo, então a ordem de chegada das
/// mensagens não altera o resultado.
/// </summary>
public sealed class SaldoDiario : AggregateRoot<DateOnly>
{
    private SaldoDiario(DateOnly data, Money totalCreditos, Money totalDebitos, DateTimeOffset atualizadoEm) : base(data)
    {
        TotalCreditos = totalCreditos;
        TotalDebitos = totalDebitos;
        AtualizadoEm = atualizadoEm;
    }

    public DateOnly Data => Id;

    public Money TotalCreditos { get; private set; }

    public Money TotalDebitos { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    /// <summary>Créditos menos débitos do dia (pode ser negativo).</summary>
    public decimal SaldoDoDia => TotalCreditos.Amount - TotalDebitos.Amount;

    public static SaldoDiario Abrir(DateOnly data, DateTimeOffset agora) =>
        new(data, Money.Zero, Money.Zero, agora);

    public static SaldoDiario Rehydrate(
        DateOnly data, decimal totalCreditos, decimal totalDebitos, DateTimeOffset atualizadoEm) =>
        new(data, Money.FromTrusted(totalCreditos), Money.FromTrusted(totalDebitos), atualizadoEm);

    public Result Aplicar(TipoMovimento tipo, decimal valor, DateTimeOffset agora)
    {
        if (!Enum.IsDefined(tipo) || valor <= 0)
        {
            return SaldoDiarioErrors.MovimentoInvalido;
        }

        var money = Money.Create(valor);
        if (money.IsFailure)
        {
            return money.Error;
        }

        if (tipo == TipoMovimento.Credito)
        {
            TotalCreditos += money.Value;
        }
        else
        {
            TotalDebitos += money.Value;
        }

        AtualizadoEm = agora;
        return Result.Success();
    }
}
