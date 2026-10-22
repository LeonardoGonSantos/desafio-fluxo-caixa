using Consolidacao.Application.Abstractions;
using Microsoft.Extensions.Caching.Hybrid;

namespace Consolidacao.External.Caching;

/// <summary>
/// Cache de leitura com HybridCache. Além do TTL curto, o principal ganho no pico é o
/// coalescimento: N requisições simultâneas para a mesma chave geram UMA consulta ao banco.
/// </summary>
public sealed class SaldoCache(HybridCache cache) : ISaldoCache
{
    public const string Tag = "saldos";

    private static readonly HybridCacheEntryOptions Options = new()
    {
        Expiration = TimeSpan.FromSeconds(5),
        LocalCacheExpiration = TimeSpan.FromSeconds(5),
    };

    public async Task<T> GetOrCreateAsync<T>(
        string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            key,
            async ct => await factory(ct),
            Options,
            tags: [Tag],
            cancellationToken: cancellationToken);

    public async Task InvalidateAllAsync(CancellationToken cancellationToken) =>
        await cache.RemoveByTagAsync(Tag, cancellationToken);
}
