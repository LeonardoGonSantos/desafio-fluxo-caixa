using System.Text;
using Lancamentos.External.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

using FluxoCaixa.BuildingBlocks.Infrastructure.Messaging;

namespace Lancamentos.External.Messaging;

public sealed class RabbitMqPublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly IOptions<RabbitMqOptions> _options;
    private readonly ResiliencePipeline _pipeline;
    private IChannel? _channel;

    public RabbitMqPublisher(
        RabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqOptions> options,
        ResiliencePipelineProvider<string> pipelines)
    {
        _connectionProvider = connectionProvider;
        _options = options;
        _pipeline = pipelines.GetPipeline(PublishResilience.PipelineName);
    }

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        await _pipeline.ExecuteAsync(
            async ct => await PublishOnceAsync(message, ct), cancellationToken);

    private async ValueTask PublishOnceAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        // Um canal com confirms não suporta publicações concorrentes sem tracking pesado:
        // o outbox publica em série, então serializamos aqui por segurança.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = message.Id.ToString(),
                Type = message.Type,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            };

            try
            {
                // mandatory + confirm tracking: se nenhuma fila estiver vinculada, o broker devolve a
                // mensagem e o client lança PublishReturnException — ela fica no outbox, não é perdida.
                await channel.BasicPublishAsync(
                    _options.Value.Exchange,
                    message.RoutingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: Encoding.UTF8.GetBytes(message.Payload),
                    cancellationToken: cancellationToken);
            }
            catch (PublishReturnException)
            {
                throw new UnroutableMessageException(message.RoutingKey);
            }
            catch
            {
                // Canal pode ter sido fechado pelo broker; força recriação na próxima tentativa.
                await DiscardChannelAsync();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await DiscardChannelAsync();
        var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await channel.ExchangeDeclareAsync(
            _options.Value.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        _channel = channel;
        return channel;
    }

    private async Task DiscardChannelAsync()
    {
        if (_channel is null)
        {
            return;
        }

        try
        {
            await _channel.DisposeAsync();
        }
        catch
        {
            // Já estava inutilizável.
        }

        _channel = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DiscardChannelAsync();
        _gate.Dispose();
    }
}
