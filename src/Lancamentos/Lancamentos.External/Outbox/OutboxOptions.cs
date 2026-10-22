using System.ComponentModel.DataAnnotations;

namespace Lancamentos.External.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Por quanto tempo uma instância "possui" as mensagens que reservou. Deve exceder o tempo esperado de publicar um lote;
    /// se expirar antes (ex.: processo morreu), outra instância reprocessa e o consumidor idempotente absorve a duplicata.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
}
