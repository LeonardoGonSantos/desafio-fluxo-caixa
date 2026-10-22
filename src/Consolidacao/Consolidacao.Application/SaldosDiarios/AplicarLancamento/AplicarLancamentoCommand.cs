using Consolidacao.Domain.SaldosDiarios;
using FluxoCaixa.BuildingBlocks.Cqrs;

namespace Consolidacao.Application.SaldosDiarios.AplicarLancamento;

public sealed record AplicarLancamentoCommand(
    Guid EventId,
    TipoMovimento Tipo,
    decimal Valor,
    DateOnly DataCompetencia) : ICommand<AplicarLancamentoResponse>;

/// <param name="Aplicado"><c>false</c> quando o evento já havia sido processado (duplicata).</param>
public sealed record AplicarLancamentoResponse(bool Aplicado);
