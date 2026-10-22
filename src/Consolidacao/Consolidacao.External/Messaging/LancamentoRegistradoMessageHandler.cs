using System.Text.Json;
using Consolidacao.Application.Abstractions;
using Consolidacao.Application.IntegrationEvents;
using Consolidacao.Application.SaldosDiarios.AplicarLancamento;
using Consolidacao.Domain.SaldosDiarios;
using FluxoCaixa.BuildingBlocks.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Consolidacao.External.Messaging;

/// <summary>
/// Decide o destino de uma mensagem <c>lancamento.registrado.v1</c>: desserializa, valida, aplica o comando e
/// invalida o cache. Não conhece RabbitMQ — só recebe bytes e devolve um <see cref="MessageOutcome"/>.
/// </summary>
public sealed class LancamentoRegistradoMessageHandler(
    IServiceScopeFactory scopeFactory,
    ILogger<LancamentoRegistradoMessageHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MessageOutcome> HandleAsync(
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
}
