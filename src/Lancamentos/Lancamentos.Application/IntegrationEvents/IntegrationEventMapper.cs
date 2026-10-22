using FluxoCaixa.BuildingBlocks.Domain;
using Lancamentos.Application.Abstractions;
using Lancamentos.Domain.Events;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Application.IntegrationEvents;

public static class IntegrationEventMapper
{
    public static LancamentoRegistradoIntegrationEvent Map(LancamentoRegistradoDomainEvent domainEvent) => new(
        domainEvent.EventId,
        domainEvent.OccurredAt,
        domainEvent.LancamentoId,
        domainEvent.Tipo.ToString(),
        domainEvent.Valor,
        domainEvent.DataCompetencia);

    /// <summary>
    /// Enfileira no outbox os eventos de domínio que viram contrato de integração e limpa os pendentes.
    /// Evento sem decisão explícita lança exceção: ignorar em silêncio faria um evento novo se perder sem ninguém notar.
    /// </summary>
    public static async Task EnqueueDomainEventsAsync(
        this IOutbox outbox, AggregateRoot<Guid> aggregate, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in aggregate.DomainEvents)
        {
            switch (domainEvent)
            {
                case LancamentoRegistradoDomainEvent registrado:
                    await outbox.EnqueueAsync(Map(registrado), cancellationToken);
                    break;

                case LancamentoEstornadoDomainEvent:
                    // Somente auditoria: o estorno chega à Consolidação como lançamento inverso.
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Evento de domínio sem mapeamento para integração: {domainEvent.GetType().Name}.");
            }
        }

        aggregate.ClearDomainEvents();
    }
}
