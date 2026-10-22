namespace Consolidacao.External.Messaging;

/// <summary>
/// Vigia a recuperação automática da conexão: se a conexão cair e não voltar dentro do prazo,
/// <see cref="Expired"/> completa e o consumidor reinicia do zero (nova conexão, topologia e consumo).
/// </summary>
public sealed class RecoveryWatchdog(TimeSpan gracePeriod, TimeProvider timeProvider) : IDisposable
{
    private readonly TaskCompletionSource _expired = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _grace;

    public Task Expired => _expired.Task;

    public void OnConnectionLost()
    {
        var grace = new CancellationTokenSource();
        Interlocked.Exchange(ref _grace, grace)?.Cancel();
        _ = ExpireAfterAsync(grace.Token);
    }

    public void OnRecovered() => Interlocked.Exchange(ref _grace, null)?.Cancel();

    public void Dispose() => OnRecovered();

    private async Task ExpireAfterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(gracePeriod, timeProvider, cancellationToken);
            _expired.TrySetResult();
        }
        catch (OperationCanceledException)
        {
            // Recuperou (ou nova queda reiniciou o prazo) antes de expirar.
        }
    }
}
