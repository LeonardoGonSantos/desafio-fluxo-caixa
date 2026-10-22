namespace Lancamentos.External.Messaging;

public sealed record OutboxMessage(Guid Id, string Type, string RoutingKey, string Payload);

public interface IMessagePublisher
{
    /// <summary>Publica e só retorna após a confirmação do broker (publisher confirms).</summary>
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}

/// <summary>Nenhuma fila está ligada à routing key: a mensagem deve permanecer no outbox.</summary>
public sealed class UnroutableMessageException(string routingKey)
    : Exception($"Nenhuma fila vinculada à routing key '{routingKey}'.");
