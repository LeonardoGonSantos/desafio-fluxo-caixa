using FluxoCaixa.BuildingBlocks.Cqrs;
using Lancamentos.Application.Common;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Application.Lancamentos.ListarLancamentos;

/// <param name="Cursor">Cursor devolvido na página anterior (<c>nextCursor</c>); omitir para a primeira página.</param>
public sealed record ListarLancamentosQuery(
    DateOnly De,
    DateOnly Ate,
    TipoLancamento? Tipo,
    int PageSize = 20,
    string? Cursor = null) : IQuery<CursorPage<LancamentoResponse>>;
