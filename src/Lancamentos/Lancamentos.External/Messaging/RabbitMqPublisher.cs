using System.Text;
using Lancamentos.External.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

using FluxoCaixa.BuildingBlocks.Infrastructure.Messaging;

namespace Lancamentos.External.Messaging;

public sealed class RabbitMqPublisher(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options) : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ResiliencePipeline _pipeline = ResiliencePipelines.CreatePublishPipeline();
    private IChannel? _channel;

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
                    options.Value.Exchange,
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
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await channel.ExchangeDeclareAsync(
            options.Value.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
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
