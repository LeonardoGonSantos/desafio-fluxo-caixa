namespace Consolidacao.Domain.SaldosDiarios;

public interface ISaldoDiarioRepository
{
    /// <summary>
    /// Obtém o saldo do dia travando a linha até o fim da transação (criando-a zerada se não existir).
    /// Consumidores concorrentes do mesmo dia são serializados: nenhuma atualização se perde.
    /// </summary>
    Task<SaldoDiario> GetForUpdateOrCreateAsync(DateOnly data, DateTimeOffset agora, CancellationToken cancellationToken);

    Task SaveAsync(SaldoDiario saldo, CancellationToken cancellationToken);
}
