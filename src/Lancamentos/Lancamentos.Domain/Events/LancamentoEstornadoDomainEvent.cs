using FluxoCaixa.BuildingBlocks.Domain;

namespace Lancamentos.Domain.Events;

/// <summary>
/// Evento de AUDITORIA: o original foi estornado. Não é publicado no broker — a Consolidação já
/// recebe o estorno como um novo <see cref="LancamentoRegistradoDomainEvent"/> (lançamento inverso).
/// </summary>
public sealed record LancamentoEstornadoDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid LancamentoOriginalId,
    Guid LancamentoEstornoId,
    string Motivo) : IDomainEvent;
