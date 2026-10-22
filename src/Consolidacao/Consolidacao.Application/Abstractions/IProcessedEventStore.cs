namespace Consolidacao.Application.Abstractions;

/// <summary>Registro de eventos já aplicados: torna o consumo idempotente (entrega at-least-once).</summary>
public interface IProcessedEventStore
{
    /// <returns><c>true</c> se o evento é novo; <c>false</c> se já havia sido processado.</returns>
    Task<bool> TryRegisterAsync(Guid eventId, DateTimeOffset processedAt, CancellationToken cancellationToken);
}
