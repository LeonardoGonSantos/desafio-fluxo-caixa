using Lancamentos.External.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lancamentos.External.Outbox;

/// <summary>
/// Publica as mensagens pendentes do outbox. Se o broker estiver fora, as mensagens
/// permanecem no banco e o processador tenta de novo com backoff: a API de lançamentos
/// segue operando normalmente.
/// </summary>
public sealed class OutboxProcessor(
    IOutboxStore store,
    IMessagePublisher publisher,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
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
    /// <exception cref="Exception">Relança a falha de publicação depois de registrá-la e liberar o restante do lote.</exception>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var batch = await store.ClaimBatchAsync(settings.BatchSize, settings.LeaseDuration, cancellationToken);

        var published = 0;
        Exception? failure = null;

        for (var i = 0; i < batch.Count && failure is null; i++)
        {
            var message = batch[i];
            try
            {
                await publisher.PublishAsync(message, cancellationToken);

                // Já foi publicada: marca mesmo durante o shutdown, para não republicar sem necessidade.
                await store.MarkProcessedAsync(message.Id, CancellationToken.None);
                published++;
            }
            catch (OperationCanceledException)
            {
                await ReleaseRemainingAsync(batch, i);
                throw;
            }
            catch (Exception ex)
            {
                await store.MarkFailedAsync(message.Id, Truncate(ex.Message), CancellationToken.None);
                await ReleaseRemainingAsync(batch, i + 1); // preserva a ordem: não segue enquanto esta falhar
                failure = ex;
            }
        }

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

    private Task ReleaseRemainingAsync(IReadOnlyList<OutboxMessage> batch, int fromIndex) =>
        store.ReleaseAsync(batch.Skip(fromIndex).Select(m => m.Id).ToList(), CancellationToken.None);

    private static string Truncate(string value) => value.Length <= 1000 ? value : value[..1000];
}
