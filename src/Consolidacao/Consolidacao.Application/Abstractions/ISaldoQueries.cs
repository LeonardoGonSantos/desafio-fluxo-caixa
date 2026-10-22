namespace Consolidacao.Application.Abstractions;

public sealed record SaldoDiaRow(DateOnly Data, decimal TotalCreditos, decimal TotalDebitos, DateTimeOffset AtualizadoEm);

/// <summary>Lado de leitura do CQRS: SQL direto, sem passar pelo agregado.</summary>
public interface ISaldoQueries
{
    Task<SaldoDiaRow?> GetDiaAsync(DateOnly data, CancellationToken cancellationToken);

    /// <summary>Soma de (créditos − débitos) de todos os dias até a data, inclusive.</summary>
    Task<decimal> GetSaldoAcumuladoAteAsync(DateOnly data, CancellationToken cancellationToken);

    Task<IReadOnlyList<SaldoDiaRow>> GetPeriodoAsync(DateOnly de, DateOnly ate, CancellationToken cancellationToken);
}
