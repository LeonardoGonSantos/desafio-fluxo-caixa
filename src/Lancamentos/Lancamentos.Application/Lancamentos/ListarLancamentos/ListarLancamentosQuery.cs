using FluxoCaixa.BuildingBlocks.Cqrs;
using Lancamentos.Application.Common;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Application.Lancamentos.ListarLancamentos;

public sealed record ListarLancamentosQuery(
    DateOnly De,
    DateOnly Ate,
    TipoLancamento? Tipo,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<LancamentoResponse>>;
