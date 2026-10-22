using Consolidacao.Application.Abstractions;
using Dapper;
using FluxoCaixa.BuildingBlocks.Infrastructure.Persistence;

namespace Consolidacao.External.Persistence.Repositories;

public sealed class ProcessedEventStore(DbSession session) : IProcessedEventStore
{
    public async Task<bool> TryRegisterAsync(Guid eventId, DateTimeOffset processedAt, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO processed_events (event_id, processed_at)
            VALUES (@eventId, @processedAt)
            ON CONFLICT (event_id) DO NOTHING
            """,
            new { eventId, processedAt },
            session.Transaction,
            cancellationToken: cancellationToken));

        return inserted == 1;
    }
}
