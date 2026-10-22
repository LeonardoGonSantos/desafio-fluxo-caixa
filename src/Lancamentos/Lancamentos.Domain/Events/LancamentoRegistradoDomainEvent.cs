using FluxoCaixa.BuildingBlocks.Domain;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Domain.Events;

public sealed record LancamentoRegistradoDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid LancamentoId,
    TipoLancamento Tipo,
    decimal Valor,
    DateOnly DataCompetencia) : IDomainEvent;
