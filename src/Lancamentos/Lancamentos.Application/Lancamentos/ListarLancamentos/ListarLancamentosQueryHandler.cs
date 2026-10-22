using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;
using Lancamentos.Application.Abstractions;
using Lancamentos.Application.Common;

namespace Lancamentos.Application.Lancamentos.ListarLancamentos;

public sealed class ListarLancamentosQueryHandler(ILancamentoQueries queries)
    : IQueryHandler<ListarLancamentosQuery, CursorPage<LancamentoResponse>>
{
    public async Task<Result<CursorPage<LancamentoResponse>>> Handle(
        ListarLancamentosQuery query, CancellationToken cancellationToken)
    {
        LancamentoCursor.TryDecode(query.Cursor, out var after); // já validado; nulo => primeira página

        // Pede uma linha a mais: se vier, existe próxima página (sem precisar de count(*)).
        var rows = await queries.ListAsync(
            query.De, query.Ate, query.Tipo, query.PageSize + 1, query.Cursor is null ? null : after, cancellationToken);

        var temMais = rows.Count > query.PageSize;
        var items = temMais ? rows.Take(query.PageSize).ToList() : rows;
        var nextCursor = temMais
            ? new LancamentoCursor(items[^1].DataCompetencia, items[^1].CriadoEm, items[^1].Id).Encode()
            : null;

        return new CursorPage<LancamentoResponse>(items, nextCursor);
    }
}
