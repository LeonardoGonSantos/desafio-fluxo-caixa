using Lancamentos.Application.Common;
using Lancamentos.Application.Lancamentos;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Application.Abstractions;

/// <summary>Lado de leitura do CQRS: projeta direto para DTO, sem passar pelo agregado.</summary>
public interface ILancamentoQueries
{
    Task<LancamentoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista em ordem decrescente de (data de competência, criação, id), a partir da posição seguinte a
    /// <paramref name="after"/> (ou do início, se nulo). Paginação por chave: custo constante em qualquer página.
    /// </summary>
    Task<IReadOnlyList<LancamentoResponse>> ListAsync(
        DateOnly de, DateOnly ate, TipoLancamento? tipo, int limit, LancamentoCursor? after, CancellationToken cancellationToken);
}
