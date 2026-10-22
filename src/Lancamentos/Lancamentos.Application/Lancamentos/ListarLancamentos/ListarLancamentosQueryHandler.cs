using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;
using Lancamentos.Application.Abstractions;
using Lancamentos.Application.Common;

namespace Lancamentos.Application.Lancamentos.ListarLancamentos;

public sealed class ListarLancamentosQueryHandler(ILancamentoQueries queries)
    : IQueryHandler<ListarLancamentosQuery, PagedResult<LancamentoResponse>>
{
    public async Task<Result<PagedResult<LancamentoResponse>>> Handle(
        ListarLancamentosQuery query, CancellationToken cancellationToken) =>
        await queries.ListAsync(query.De, query.Ate, query.Tipo, query.Page, query.PageSize, cancellationToken);
}
