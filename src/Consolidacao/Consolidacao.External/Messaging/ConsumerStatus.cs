namespace Consolidacao.External.Messaging;

/// <summary>Estado do consumo, exposto para health check.</summary>
public sealed class ConsumerStatus
{
    private volatile bool _isConsuming;

    public bool IsConsuming
    {
        get => _isConsuming;
        set => _isConsuming = value;
    }
}
