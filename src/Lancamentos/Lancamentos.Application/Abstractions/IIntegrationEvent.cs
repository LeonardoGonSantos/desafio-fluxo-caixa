namespace Lancamentos.Application.Abstractions;

/// <summary>Contrato público publicado para outros serviços (versionado pelo <see cref="EventType"/>).</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }

    string EventType { get; }
}
