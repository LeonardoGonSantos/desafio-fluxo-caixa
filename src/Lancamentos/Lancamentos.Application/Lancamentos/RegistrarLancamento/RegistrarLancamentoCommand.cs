using FluxoCaixa.BuildingBlocks.Cqrs;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Application.Lancamentos.RegistrarLancamento;

public sealed record RegistrarLancamentoCommand(
    TipoLancamento Tipo,
    decimal Valor,
    string Descricao,
    DateOnly DataCompetencia) : ICommand<LancamentoResponse>;
