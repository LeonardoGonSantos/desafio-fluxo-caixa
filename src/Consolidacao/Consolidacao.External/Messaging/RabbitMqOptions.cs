using System.ComponentModel.DataAnnotations;
using FluxoCaixa.BuildingBlocks.Infrastructure.Messaging;

namespace Consolidacao.External.Messaging;

public sealed class RabbitMqOptions : RabbitMqConnectionOptions
{
    /// <summary>Exchange publicado pelo serviço de Lançamentos.</summary>
    [Required]
    public string Exchange { get; set; } = "fluxocaixa.lancamentos";

    [Required]
    public string RoutingKey { get; set; } = "lancamento.registrado.v1";

    [Required]
    public string Queue { get; set; } = "consolidacao.lancamentos";

    [Required]
    public string DeadLetterExchange { get; set; } = "fluxocaixa.dlx";

    [Required]
    public string DeadLetterQueue { get; set; } = "consolidacao.lancamentos.dlq";

    /// <summary>Mensagens não confirmadas por consumidor ao mesmo tempo.</summary>
    [Range(1, 1000)]
    public ushort PrefetchCount { get; set; } = 20;

    /// <summary>Processamentos paralelos de mensagens.</summary>
    [Range(1, 64)]
    public ushort Concurrency { get; set; } = 4;

    /// <summary>
    /// Tentativas de processamento antes de enviar a mensagem para a DLQ. A contagem é feita pela aplicação (header
    /// <c>x-retry-count</c>): no RabbitMQ 4, <c>nack(requeue)</c> NÃO conta para o <c>x-delivery-limit</c> da quorum queue.
    /// </summary>
    [Range(1, 100)]
    public int MaxProcessingAttempts { get; set; } = 5;

    /// <summary>Espera base entre tentativas; cresce linearmente (atraso × número da tentativa).</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Quanto esperar a recuperação automática do client depois de uma queda antes de recriar conexão e consumo do zero.
    /// </summary>
    public TimeSpan RecoveryGracePeriod { get; set; } = TimeSpan.FromSeconds(30);
}
