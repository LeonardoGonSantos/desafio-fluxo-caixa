using Consolidacao.External.Messaging;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Consolidacao.External.Tests;

public class RecoveryWatchdogTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void SemQueda_NaoExpira()
    {
        using var watchdog = new RecoveryWatchdog(Grace, _time);

        _time.Advance(TimeSpan.FromHours(1));

        watchdog.Expired.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public void QuedaSemRecuperacao_ExpiraAposOPrazo()
    {
        using var watchdog = new RecoveryWatchdog(Grace, _time);

        watchdog.OnConnectionLost();
        _time.Advance(Grace - TimeSpan.FromSeconds(1));
        watchdog.Expired.IsCompleted.ShouldBeFalse();

        _time.Advance(TimeSpan.FromSeconds(1));
        watchdog.Expired.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void QuedaSeguidaDeRecuperacaoDentroDoPrazo_NuncaExpira()
    {
        using var watchdog = new RecoveryWatchdog(Grace, _time);

        watchdog.OnConnectionLost();
        _time.Advance(TimeSpan.FromSeconds(10));
        watchdog.OnRecovered();
        _time.Advance(TimeSpan.FromMinutes(10));

        watchdog.Expired.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public void NovaQuedaReiniciaOPrazo()
    {
        using var watchdog = new RecoveryWatchdog(Grace, _time);

        watchdog.OnConnectionLost();
        _time.Advance(TimeSpan.FromSeconds(20));
        watchdog.OnConnectionLost(); // outra queda: o prazo recomeça
        _time.Advance(TimeSpan.FromSeconds(20));
        watchdog.Expired.IsCompleted.ShouldBeFalse();

        _time.Advance(TimeSpan.FromSeconds(10));
        watchdog.Expired.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_CancelaOPrazoPendente()
    {
        var watchdog = new RecoveryWatchdog(Grace, _time);
        watchdog.OnConnectionLost();

        watchdog.Dispose();
        _time.Advance(TimeSpan.FromMinutes(5));

        watchdog.Expired.IsCompleted.ShouldBeFalse();
    }
}
