using FluxoCaixa.BuildingBlocks.Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Consolidacao.External.Messaging;

/// <summary>
/// Ciclo de vida do consumo: conecta, declara a topologia, consome e confirma/rejeita conforme o resultado.
/// Reinicia sozinho se a conexão cair e a recuperação automática do client não restaurá-la a tempo.
/// </summary>
public sealed class RabbitMqConsumerHostedService(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options,
    LancamentoRegistradoMessageHandler messageHandler,
    ConsumerStatus status,
    TimeProvider timeProvider,
    ILogger<RabbitMqConsumerHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        while (!stoppingToken.IsCancellationRequested)
        {
            using var watchdog = new RecoveryWatchdog(options.Value.RecoveryGracePeriod, timeProvider);
            IChannel? channel = null;
            try
            {
                channel = await StartConsumingAsync(watchdog, stoppingToken);
                status.IsConsuming = true;

                await watchdog.Expired.WaitAsync(stoppingToken);
                logger.LogWarning("Conexão com o broker não se recuperou em {Grace}; reiniciando o consumo",
                    options.Value.RecoveryGracePeriod);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Consumer não conseguiu iniciar (broker indisponível?); nova tentativa em {Delay}s",
                    RestartDelay.TotalSeconds);
            }
            finally
            {
                status.IsConsuming = false;
                await DisposeQuietlyAsync(channel);
            }

            try
            {
                await Task.Delay(RestartDelay, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<IChannel> StartConsumingAsync(RecoveryWatchdog watchdog, CancellationToken cancellationToken)
    {
        var o = options.Value;
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                consumerDispatchConcurrency: o.Concurrency),
            cancellationToken);

        // Quedas de conexão/canal iniciam a contagem do prazo; a recuperação automática a cancela.
        connection.ConnectionShutdownAsync += (_, args) => OnShutdown(args, watchdog);
        connection.RecoverySucceededAsync += (_, _) => OnRecovered(watchdog);
        channel.ChannelShutdownAsync += (_, args) => OnShutdown(args, watchdog);

        await ConsolidacaoTopology.DeclareAsync(channel, o, cancellationToken);
        await channel.BasicQosAsync(0, o.PrefetchCount, false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += OnReceivedAsync;
        await channel.BasicConsumeAsync(o.Queue, autoAck: false, consumer, cancellationToken);

        logger.LogInformation("Consumindo a fila {Queue} (prefetch {Prefetch}, concorrência {Concurrency})",
            o.Queue, o.PrefetchCount, o.Concurrency);
        return channel;
    }

    private Task OnShutdown(ShutdownEventArgs args, RecoveryWatchdog watchdog)
    {
        if (args.Initiator != ShutdownInitiator.Application)
        {
            logger.LogWarning("Conexão/canal com o broker caiu ({Reason}); aguardando recuperação automática",
                args.ReplyText);
            status.IsConsuming = false;
            watchdog.OnConnectionLost();
        }

        return Task.CompletedTask;
    }

    private Task OnRecovered(RecoveryWatchdog watchdog)
    {
        logger.LogInformation("Conexão com o broker recuperada; consumo retomado");
        watchdog.OnRecovered();
        status.IsConsuming = true;
        return Task.CompletedTask;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var channel = ((AsyncEventingBasicConsumer)sender).Channel;
        var outcome = await messageHandler.HandleAsync(args.Body, args.BasicProperties.Type, _stoppingToken);

        switch (outcome)
        {
            case MessageOutcome.Ack:
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, CancellationToken.None);
                break;
            case MessageOutcome.Reject:
                await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
                break;
            default:
                await RetryOrDeadLetterAsync(channel, args);
                break;
        }
    }

    /// <summary>
    /// Falha transitória: reenfileira a mensagem com o contador de tentativas incrementado, ou a envia para a DLQ
    /// ao esgotar <see cref="RabbitMqOptions.MaxProcessingAttempts"/>. (nack+requeue simples reentregaria para sempre:
    /// no RabbitMQ 4 ele não conta para o delivery-limit.)
    /// </summary>
    private async Task RetryOrDeadLetterAsync(IChannel channel, BasicDeliverEventArgs args)
    {
        var o = options.Value;

        if (_stoppingToken.IsCancellationRequested)
        {
            // Encerrando: devolve sem contar como tentativa.
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
            return;
        }

        var attempt = RetryHeaders.ReadRetryCount(args.BasicProperties.Headers) + 1;
        if (attempt >= o.MaxProcessingAttempts)
        {
            logger.LogError("Mensagem {MessageId} esgotou {Attempts} tentativas; enviada para a DLQ",
                args.BasicProperties.MessageId, attempt);
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            return;
        }

        await DelayAsync(o.RetryDelay * attempt);

        try
        {
            var headers = new Dictionary<string, object?>(args.BasicProperties.Headers ?? new Dictionary<string, object?>())
            {
                [RetryHeaders.RetryCount] = attempt,
            };
            var properties = new BasicProperties(args.BasicProperties) { Headers = headers, Persistent = true };

            // Publica de volta na mesma fila e SÓ ENTÃO confirma a original: se algo falhar no meio, pode haver duplicata
            // (absorvida pela idempotência do consumidor), mas nunca perda.
            await channel.BasicPublishAsync(string.Empty, o.Queue, mandatory: true, properties, args.Body, CancellationToken.None);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível reenfileirar a mensagem {MessageId}; devolvendo à fila",
                args.BasicProperties.MessageId);
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
        }
    }

    private async Task DelayAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, timeProvider, _stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Encerrando: devolve a mensagem imediatamente.
        }
    }

    private static async Task DisposeQuietlyAsync(IChannel? channel)
    {
        if (channel is null)
        {
            return;
        }

        try
        {
            await channel.DisposeAsync();
        }
        catch
        {
            // O canal já estava inutilizável.
        }
    }
}
