namespace Lancamentos.Application.Abstractions;

public sealed record IdempotentResponse(int StatusCode, string Body, string? Location);

public enum ReservationOutcome
{
    /// <summary>Chave nova (ou reivindicada de uma execução abandonada): o chamador deve executar a operação.</summary>
    Acquired = 1,

    /// <summary>Mesma chave e mesmo corpo já concluídos: reproduzir a resposta guardada.</summary>
    Completed = 2,

    /// <summary>Outra requisição com a mesma chave ainda está em andamento.</summary>
    InProgress = 3,

    /// <summary>A chave já foi usada com um corpo diferente.</summary>
    HashMismatch = 4,
}

public sealed record IdempotencyReservation(ReservationOutcome Outcome, IdempotentResponse? Response = null);

/// <summary>
/// Garante que repetições do mesmo POST (mesma Idempotency-Key) não dupliquem o efeito.
/// Reserva a chave ANTES de executar, evitando a corrida entre requisições simultâneas.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyReservation> ReserveAsync(string key, string requestHash, CancellationToken cancellationToken);

    Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken);

    /// <summary>Libera a chave (ex.: após erro 5xx) para que o cliente possa tentar de novo.</summary>
    Task ReleaseAsync(string key, CancellationToken cancellationToken);
}
