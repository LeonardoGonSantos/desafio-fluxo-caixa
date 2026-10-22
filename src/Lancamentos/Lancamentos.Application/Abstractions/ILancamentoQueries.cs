using Lancamentos.Application.Common;
using Lancamentos.Application.Lancamentos;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Application.Abstractions;

/// <summary>Lado de leitura do CQRS: projeta direto para DTO, sem passar pelo agregado.</summary>
public interface ILancamentoQueries
{
    Task<LancamentoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<LancamentoResponse>> ListAsync(
        DateOnly de, DateOnly ate, TipoLancamento? tipo, int page, int pageSize, CancellationToken cancellationToken);
}
