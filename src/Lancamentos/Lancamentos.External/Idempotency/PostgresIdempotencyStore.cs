using Dapper;
using Lancamentos.Application.Abstractions;
using Npgsql;

namespace Lancamentos.External.Idempotency;

public sealed class PostgresIdempotencyStore(NpgsqlDataSource dataSource, TimeProvider timeProvider) : IIdempotencyStore
{
    /// <summary>Reservas "em andamento" mais antigas que isto são consideradas abandonadas.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(60);

    private const string ReserveSql = """
        INSERT INTO idempotency_keys (key, request_hash, status, created_at)
        VALUES (@key, @requestHash, 0, @now)
        ON CONFLICT (key) DO UPDATE
            SET request_hash = EXCLUDED.request_hash, created_at = EXCLUDED.created_at
            WHERE idempotency_keys.status = 0
              AND idempotency_keys.created_at < @staleBefore
        """;

    private const string SelectSql = """
        SELECT request_hash, status, response_status, response_body, response_location
        FROM idempotency_keys WHERE key = @key
        """;

    /// <summary>Quantas vezes repetir se a chave for liberada entre o INSERT e o SELECT (corrida rara).</summary>
    private const int MaxReserveAttempts = 3;

    public async Task<IdempotencyReservation> ReserveAsync(
        string key, string requestHash, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        for (var attempt = 0; attempt < MaxReserveAttempts; attempt++)
        {
            var now = timeProvider.GetUtcNow();
            var inserted = await connection.ExecuteAsync(new CommandDefinition(
                ReserveSql, new { key, requestHash, now, staleBefore = now - StaleAfter },
                cancellationToken: cancellationToken));
            if (inserted == 1)
            {
                return new IdempotencyReservation(ReservationOutcome.Acquired);
            }

            var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
                SelectSql, new { key }, cancellationToken: cancellationToken));
            if (row is null)
            {
                continue; // liberada entre o INSERT e o SELECT: tenta de novo
            }

            if (!string.Equals(row.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return new IdempotencyReservation(ReservationOutcome.HashMismatch);
            }

            return row.Status == 1
                ? new IdempotencyReservation(
                    ReservationOutcome.Completed,
                    new IdempotentResponse(row.ResponseStatus!.Value, row.ResponseBody!, row.ResponseLocation))
                : new IdempotencyReservation(ReservationOutcome.InProgress);
        }

        // Esgotou as tentativas sem estado estável: o cliente repete a chamada (409).
        return new IdempotencyReservation(ReservationOutcome.InProgress);
    }

    public async Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE idempotency_keys
            SET status = 1, response_status = @StatusCode, response_body = @Body, response_location = @Location
            WHERE key = @key
            """,
            new { key, response.StatusCode, response.Body, response.Location },
            cancellationToken: cancellationToken));
    }

    public async Task ReleaseAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM idempotency_keys WHERE key = @key AND status = 0",
            new { key },
            cancellationToken: cancellationToken));
    }

    private sealed record Row(
        string RequestHash, short Status, int? ResponseStatus, string? ResponseBody, string? ResponseLocation);
}
