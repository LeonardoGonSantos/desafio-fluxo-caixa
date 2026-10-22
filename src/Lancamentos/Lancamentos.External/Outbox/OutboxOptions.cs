using System.ComponentModel.DataAnnotations;

namespace Lancamentos.External.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;
}
