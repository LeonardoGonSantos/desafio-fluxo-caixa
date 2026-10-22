using FluxoCaixa.BuildingBlocks.Cqrs;

namespace Lancamentos.Application.Lancamentos.ObterLancamentoPorId;

public sealed record ObterLancamentoPorIdQuery(Guid Id) : IQuery<LancamentoResponse>;
