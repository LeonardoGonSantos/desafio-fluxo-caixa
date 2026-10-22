using Dapper;
using Lancamentos.External.Messaging;
using Npgsql;

namespace Lancamentos.External.Outbox;

public sealed class PostgresOutboxStore(NpgsqlDataSource dataSource) : IOutboxStore
{
    // Usa o relógio do banco (now()) para evitar divergência entre instâncias da aplicação.
    private const string ClaimSql = """
        UPDATE outbox_messages
        SET locked_until = now() + (@leaseSeconds * interval '1 second')
        WHERE id IN (
            SELECT id FROM outbox_messages
            WHERE processed_at IS NULL
              AND (locked_until IS NULL OR locked_until < now())
            ORDER BY occurred_at
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED)
        RETURNING id, type, routing_key, payload::text AS payload, occurred_at
        """;

    public async Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
        int batchSize, TimeSpan lease, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            ClaimSql,
            new { batchSize, leaseSeconds = lease.TotalSeconds },
            cancellationToken: cancellationToken));

        // RETURNING não garante ordem: restabelece a ordem de ocorrência.
        return rows
            .OrderBy(r => r.OccurredAt)
            .Select(r => new OutboxMessage(r.Id, r.Type, r.RoutingKey, r.Payload))
            .ToList();
    }

    public async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE outbox_messages
            SET processed_at = now(), attempts = attempts + 1, last_error = NULL, locked_until = NULL
            WHERE id = @id
            """,
            new { id },
            cancellationToken: cancellationToken));
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE outbox_messages SET attempts = attempts + 1, last_error = @error, locked_until = NULL WHERE id = @id",
            new { id, error },
            cancellationToken: cancellationToken));
    }

    public async Task ReleaseAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE outbox_messages SET locked_until = NULL WHERE id = ANY(@ids) AND processed_at IS NULL",
            new { ids = ids.ToArray() },
            cancellationToken: cancellationToken));
    }

    private sealed record Row(Guid Id, string Type, string RoutingKey, string Payload, DateTime OccurredAt);
}
