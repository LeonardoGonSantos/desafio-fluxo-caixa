using System.ComponentModel.DataAnnotations;

namespace Consolidacao.External.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required]
    public string Username { get; set; } = "guest";

    [Required]
    public string Password { get; set; } = "guest";

    [Required]
    public string VirtualHost { get; set; } = "/";

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

    /// <summary>Entregas máximas antes de a mensagem ir para a DLQ (quorum queue delivery-limit).</summary>
    [Range(1, 100)]
    public int DeliveryLimit { get; set; } = 5;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
}
