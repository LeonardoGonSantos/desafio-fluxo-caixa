namespace Lancamentos.Application.Abstractions;

/// <summary>Enfileira eventos na MESMA transação do agregado (Transactional Outbox).</summary>
public interface IOutbox
{
    Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent;
}
