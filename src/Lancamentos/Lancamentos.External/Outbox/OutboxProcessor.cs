using Dapper;
using Lancamentos.External.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Lancamentos.External.Outbox;

/// <summary>
/// Publica as mensagens pendentes do outbox. Se o broker estiver fora, as mensagens
/// permanecem no banco e o processador tenta de novo com backoff: a API de lançamentos
/// segue operando normalmente.
/// </summary>
public sealed class OutboxProcessor(
    NpgsqlDataSource dataSource,
    IMessagePublisher publisher,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private const string SelectPendingSql = """
        SELECT id, type, routing_key, payload::text AS payload
        FROM outbox_messages
        WHERE processed_at IS NULL
        ORDER BY occurred_at
        LIMIT @batchSize
        FOR UPDATE SKIP LOCKED
        """;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                consecutiveFailures = 0;
                if (processed < settings.BatchSize)
                {
                    await Task.Delay(settings.PollingInterval, timeProvider, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(consecutiveFailures, 5))));
                logger.LogWarning(ex, "Falha ao processar o outbox (falha consecutiva {Failures}); nova tentativa em {Delay}s",
                    consecutiveFailures, delay.TotalSeconds);
                try
                {
                    await Task.Delay(delay, timeProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Processa um lote. Retorna quantas mensagens foram publicadas.</summary>
    /// <exception cref="Exception">Relança a falha de publicação após registrar a tentativa no banco.</exception>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var pending = (await connection.QueryAsync<PendingRow>(new CommandDefinition(
            SelectPendingSql, new { batchSize = options.Value.BatchSize }, transaction,
            cancellationToken: cancellationToken))).ToList();

        var published = 0;
        Exception? failure = null;

        foreach (var row in pending)
        {
            try
            {
                await publisher.PublishAsync(
                    new OutboxMessage(row.Id, row.Type, row.RoutingKey, row.Payload), cancellationToken);

                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE outbox_messages SET processed_at = @now, attempts = attempts + 1, last_error = NULL WHERE id = @id",
                    new { id = row.Id, now = timeProvider.GetUtcNow() }, transaction,
                    cancellationToken: cancellationToken));
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE outbox_messages SET attempts = attempts + 1, last_error = @error WHERE id = @id",
                    new { id = row.Id, error = Truncate(ex.Message) }, transaction,
                    cancellationToken: cancellationToken));
                failure = ex;
                break; // preserva a ordem: não publica as seguintes se esta falhou
            }
        }

        await transaction.CommitAsync(cancellationToken);

        if (failure is not null)
        {
            throw failure;
        }

        if (published > 0)
        {
            logger.LogInformation("Outbox: {Count} mensagem(ns) publicada(s)", published);
        }

        return published;
    }

    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000];

    private sealed record PendingRow(Guid Id, string Type, string RoutingKey, string Payload);
}
