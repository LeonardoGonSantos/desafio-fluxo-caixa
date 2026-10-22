using Lancamentos.Application.Abstractions;

namespace Lancamentos.Application.IntegrationEvents;

/// <summary>Contrato v1 consumido pela Consolidação. Mudanças incompatíveis exigem novo EventType (v2).</summary>
public sealed record LancamentoRegistradoIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid LancamentoId,
    string Tipo,
    decimal Valor,
    DateOnly DataCompetencia) : IIntegrationEvent
{
    public const string Type = "lancamento.registrado.v1";

    public string EventType => Type;
}
