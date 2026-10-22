using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;
using Lancamentos.Application.Abstractions;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Application.Lancamentos.ObterLancamentoPorId;

public sealed class ObterLancamentoPorIdQueryHandler(ILancamentoQueries queries)
    : IQueryHandler<ObterLancamentoPorIdQuery, LancamentoResponse>
{
    public async Task<Result<LancamentoResponse>> Handle(
        ObterLancamentoPorIdQuery query, CancellationToken cancellationToken)
    {
        var lancamento = await queries.GetByIdAsync(query.Id, cancellationToken);
        return lancamento is null ? LancamentoErrors.NaoEncontrado : lancamento;
    }
}
