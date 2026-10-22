namespace Lancamentos.Domain.Lancamentos;

public interface ILancamentoRepository
{
    Task AddAsync(Lancamento lancamento, CancellationToken cancellationToken);

    /// <summary>
    /// Obtém o lançamento travando a linha (FOR UPDATE) até o fim da transação,
    /// serializando estornos concorrentes do mesmo lançamento.
    /// </summary>
    Task<Lancamento?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Persiste a mudança de status (único atributo mutável de um lançamento).</summary>
    Task UpdateStatusAsync(Lancamento lancamento, CancellationToken cancellationToken);
}
