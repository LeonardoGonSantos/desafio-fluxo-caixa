using RabbitMQ.Client;

namespace Consolidacao.External.Messaging;

/// <summary>
/// A Consolidação é dona da sua fila: declara exchange, fila, DLQ e bindings (idempotente).
/// Quorum queue com dead-letter para a DLQ. As tentativas de reprocessamento são contadas pela aplicação
/// (<see cref="RetryHeaders"/>), pois o <c>x-delivery-limit</c> não conta <c>nack(requeue)</c> no RabbitMQ 4.
/// </summary>
public static class ConsolidacaoTopology
{
    public static async Task DeclareAsync(IChannel channel, RabbitMqOptions o, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(o.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(o.DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            o.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(o.DeadLetterQueue, o.DeadLetterExchange, o.DeadLetterQueue,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            o.Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = o.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = o.DeadLetterQueue,
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(o.Queue, o.Exchange, o.RoutingKey, cancellationToken: cancellationToken);
    }
}
