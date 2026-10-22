using System.ComponentModel.DataAnnotations;

namespace FluxoCaixa.BuildingBlocks.Infrastructure.Messaging;

/// <summary>Parâmetros de conexão com o broker. As opções de cada serviço estendem esta classe.</summary>
public class RabbitMqConnectionOptions
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
}
