namespace Consolidacao.Application.Abstractions;

public interface ISaldoCache
{
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken);

    /// <summary>
    /// Invalida todas as consultas em cache. Um lançamento retroativo altera o saldo acumulado
    /// de todos os dias seguintes, então invalidar só o dia afetado não basta.
    /// </summary>
    Task InvalidateAllAsync(CancellationToken cancellationToken);
}
