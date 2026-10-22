using FluxoCaixa.BuildingBlocks.Cqrs;

namespace Lancamentos.Application.Lancamentos.EstornarLancamento;

public sealed record EstornarLancamentoCommand(Guid LancamentoId, string Motivo) : ICommand<LancamentoResponse>;
