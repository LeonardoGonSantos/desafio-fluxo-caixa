using System.Text.Json;
using Dapper;
using Lancamentos.Application.Abstractions;

namespace Lancamentos.External.Persistence.Repositories;

public sealed class OutboxRepository(DbSession session) : IOutbox
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string InsertSql = """
        INSERT INTO outbox_messages (id, type, routing_key, payload, occurred_at)
        VALUES (@Id, @Type, @RoutingKey, @Payload::jsonb, @OccurredAt)
        """;

    public async Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new
            {
                Id = integrationEvent.EventId,
                Type = integrationEvent.EventType,
                RoutingKey = integrationEvent.EventType,
                Payload = JsonSerializer.Serialize(integrationEvent, JsonOptions),
                OccurredAt = integrationEvent.OccurredAt,
            },
            session.Transaction,
            cancellationToken: cancellationToken));
    }
}
