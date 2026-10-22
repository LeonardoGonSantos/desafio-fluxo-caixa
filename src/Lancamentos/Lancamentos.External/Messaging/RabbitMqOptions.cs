using System.ComponentModel.DataAnnotations;
using FluxoCaixa.BuildingBlocks.Infrastructure.Messaging;

namespace Lancamentos.External.Messaging;

public sealed class RabbitMqOptions : RabbitMqConnectionOptions
{
    [Required]
    public string Exchange { get; set; } = "fluxocaixa.lancamentos";
}
