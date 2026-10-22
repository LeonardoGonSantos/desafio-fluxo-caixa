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

    /// <summary>Converte e enfileira no outbox todos os eventos pendentes do agregado.</summary>
    public static async Task EnqueueDomainEventsAsync(
        this IOutbox outbox, AggregateRoot<Guid> aggregate, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in aggregate.DomainEvents)
        {
            if (domainEvent is LancamentoRegistradoDomainEvent registrado)
            {
                await outbox.EnqueueAsync(Map(registrado), cancellationToken);
            }
        }

        aggregate.ClearDomainEvents();
    }
}
