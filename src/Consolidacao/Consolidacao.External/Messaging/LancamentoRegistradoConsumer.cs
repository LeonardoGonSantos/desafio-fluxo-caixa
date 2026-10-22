using System.Text.Json;
using Consolidacao.Application.Abstractions;
using Consolidacao.Application.IntegrationEvents;
using Consolidacao.Application.SaldosDiarios;
using Consolidacao.Application.SaldosDiarios.AplicarLancamento;
using Consolidacao.Domain.SaldosDiarios;
using FluxoCaixa.BuildingBlocks.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using FluxoCaixa.BuildingBlocks.Infrastructure.Messaging;

namespace Consolidacao.External.Messaging;

public enum MessageOutcome
{
    /// <summary>Processada (ou duplicata): confirma e remove da fila.</summary>
    Ack = 1,

    /// <summary>Mensagem inválida ("poison"): vai direto para a DLQ, sem novas tentativas.</summary>
    Reject = 2,

    /// <summary>Falha transitória: devolve à fila; após o delivery-limit o broker a envia para a DLQ.</summary>
    Retry = 3,
}

/// <summary>
/// Consome LancamentoRegistrado e atualiza o saldo diário de forma idempotente.
/// Entrega at-least-once + registro de eventos processados na mesma transação = efeito exatamente-uma-vez.
/// </summary>
public sealed class LancamentoRegistradoConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<LancamentoRegistradoConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        IChannel? channel = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                channel = await StartConsumingAsync(stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Consumer não conseguiu iniciar (broker indisponível?); nova tentativa em 5s");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (channel is not null)
        {
            await channel.DisposeAsync();
        }
    }

    private async Task<IChannel> StartConsumingAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false,
                consumerDispatchConcurrency: o.Concurrency),
            cancellationToken);

        await DeclareTopologyAsync(channel, o, cancellationToken);
        await channel.BasicQosAsync(0, o.PrefetchCount, false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += OnReceivedAsync;
        await channel.BasicConsumeAsync(o.Queue, autoAck: false, consumer, cancellationToken);

        logger.LogInformation("Consumindo a fila {Queue} (prefetch {Prefetch}, concorrência {Concurrency})",
            o.Queue, o.PrefetchCount, o.Concurrency);
        return channel;
    }

    /// <summary>
    /// A Consolidação é dona da sua fila: declara fila, DLQ e bindings (idempotente).
    /// Quorum queue com delivery-limit: reentregas limitadas e dead-letter automático.
    /// </summary>
    public static async Task DeclareTopologyAsync(IChannel channel, RabbitMqOptions o, CancellationToken cancellationToken)
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
                ["x-delivery-limit"] = o.DeliveryLimit,
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(o.Queue, o.Exchange, o.RoutingKey, cancellationToken: cancellationToken);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var channel = ((AsyncEventingBasicConsumer)sender).Channel;
        var outcome = await ProcessAsync(args.Body, args.BasicProperties.Type, _stoppingToken);

        switch (outcome)
        {
            case MessageOutcome.Ack:
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, CancellationToken.None);
                break;
            case MessageOutcome.Reject:
                await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
                break;
            default:
                await DelayAsync(options.Value.RetryDelay);
                await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
                break;
        }
    }

    public async Task<MessageOutcome> ProcessAsync(
        ReadOnlyMemory<byte> body, string? messageType, CancellationToken cancellationToken)
    {
        LancamentoRegistradoIntegrationEvent? evento;
        try
        {
            evento = JsonSerializer.Deserialize<LancamentoRegistradoIntegrationEvent>(body.Span, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Mensagem com JSON inválido enviada para a DLQ");
            return MessageOutcome.Reject;
        }

        if (evento is null
            || evento.EventId == Guid.Empty
            || messageType != LancamentoRegistradoIntegrationEvent.Type
            || !Enum.TryParse<TipoMovimento>(evento.Tipo, ignoreCase: true, out var tipo)
            || !Enum.IsDefined(tipo))
        {
            logger.LogError("Mensagem inválida ou de tipo desconhecido ({Type}) enviada para a DLQ", messageType);
            return MessageOutcome.Reject;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider
                .GetRequiredService<ICommandHandler<AplicarLancamentoCommand, AplicarLancamentoResponse>>();

            var result = await handler.Handle(
                new AplicarLancamentoCommand(evento.EventId, tipo, evento.Valor, evento.DataCompetencia),
                cancellationToken);

            if (result.IsFailure)
            {
                logger.LogError("Evento {EventId} rejeitado pelo domínio ({Code}); enviado para a DLQ",
                    evento.EventId, result.Error.Code);
                return MessageOutcome.Reject;
            }

            if (result.Value.Aplicado)
            {
                // Só após o commit (o decorator de transação já concluiu): evita repopular o cache com dado velho.
                await scope.ServiceProvider.GetRequiredService<ISaldoCache>().InvalidateAllAsync(cancellationToken);
            }
            else
            {
                logger.LogInformation("Evento {EventId} duplicado ignorado", evento.EventId);
            }

            return MessageOutcome.Ack;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha transitória ao processar o evento {EventId}; será reentregue", evento.EventId);
            return MessageOutcome.Retry;
        }
    }

    private async Task DelayAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Encerrando: devolve a mensagem imediatamente.
        }
    }
}
