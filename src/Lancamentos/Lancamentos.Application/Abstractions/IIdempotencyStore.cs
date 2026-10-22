namespace Lancamentos.Application.Abstractions;

public sealed record IdempotentResponse(string RequestHash, int StatusCode, string Body, string? Location);

public interface IIdempotencyStore
{
    Task<IdempotentResponse?> GetAsync(string key, CancellationToken cancellationToken);

    /// <returns><c>false</c> se outra requisição já gravou a mesma chave.</returns>
    Task<bool> TrySaveAsync(string key, IdempotentResponse response, CancellationToken cancellationToken);
}
