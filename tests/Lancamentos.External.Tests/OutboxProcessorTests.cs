using Lancamentos.External.Messaging;
using Lancamentos.External.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Lancamentos.External.Tests;

public class OutboxProcessorTests
{
    private readonly IOutboxStore _store = Substitute.For<IOutboxStore>();
    private readonly IMessagePublisher _publisher = Substitute.For<IMessagePublisher>();
    private readonly OutboxProcessor _processor;

    public OutboxProcessorTests()
    {
        var options = Options.Create(new OutboxOptions { BatchSize = 10, LeaseDuration = TimeSpan.FromSeconds(30) });
        _processor = new OutboxProcessor(_store, _publisher, options, TimeProvider.System, NullLogger<OutboxProcessor>.Instance);
    }

    private static OutboxMessage Msg(int n) => new(Guid.NewGuid(), "lancamento.registrado.v1", "lancamento.registrado.v1", $"{{\"n\":{n}}}");

    private void Pendentes(params OutboxMessage[] mensagens) =>
        _store.ClaimBatchAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(mensagens);

    [Fact]
    public async Task ProcessBatch_SemPendentes_RetornaZeroENaoPublica()
    {
        Pendentes();

        var published = await _processor.ProcessBatchAsync(CancellationToken.None);

        published.ShouldBe(0);
        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(default!, default);
    }

    [Fact]
    public async Task ProcessBatch_UsaOTamanhoDoLoteEOLeaseConfigurados()
    {
        Pendentes();

        await _processor.ProcessBatchAsync(CancellationToken.None);

        await _store.Received(1).ClaimBatchAsync(10, TimeSpan.FromSeconds(30), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatch_Sucesso_PublicaEMarcaCadaMensagemNaOrdem()
    {
        var (a, b) = (Msg(1), Msg(2));
        Pendentes(a, b);

        var published = await _processor.ProcessBatchAsync(CancellationToken.None);

        published.ShouldBe(2);
        Received.InOrder(() =>
        {
            _publisher.PublishAsync(a, Arg.Any<CancellationToken>());
            _store.MarkProcessedAsync(a.Id, Arg.Any<CancellationToken>());
            _publisher.PublishAsync(b, Arg.Any<CancellationToken>());
            _store.MarkProcessedAsync(b.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task ProcessBatch_FalhaNaSegunda_RegistraFalha_LiberaOResto_NaoPublicaATerceira_ERelanca()
    {
        var (a, b, c) = (Msg(1), Msg(2), Msg(3));
        Pendentes(a, b, c);
        _publisher.PublishAsync(b, Arg.Any<CancellationToken>()).Returns(_ => throw new InvalidOperationException("broker fora"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => _processor.ProcessBatchAsync(CancellationToken.None));

        ex.Message.ShouldBe("broker fora");
        await _store.Received(1).MarkProcessedAsync(a.Id, Arg.Any<CancellationToken>());
        await _store.Received(1).MarkFailedAsync(b.Id, "broker fora", Arg.Any<CancellationToken>());
        await _store.Received(1).ReleaseAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == c.Id), Arg.Any<CancellationToken>());
        await _publisher.DidNotReceive().PublishAsync(c, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatch_MensagemSemRota_RegistraFalhaELiberaOResto()
    {
        var (a, b) = (Msg(1), Msg(2));
        Pendentes(a, b);
        _publisher.PublishAsync(a, Arg.Any<CancellationToken>()).Returns(_ => throw new UnroutableMessageException(a.RoutingKey));

        await Should.ThrowAsync<UnroutableMessageException>(() => _processor.ProcessBatchAsync(CancellationToken.None));

        await _store.Received(1).MarkFailedAsync(a.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _store.Received(1).ReleaseAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == b.Id), Arg.Any<CancellationToken>());
        await _store.DidNotReceiveWithAnyArgs().MarkProcessedAsync(default, default);
    }

    [Fact]
    public async Task ProcessBatch_Cancelamento_LiberaAsNaoPublicadas_SemRegistrarFalha()
    {
        var (a, b) = (Msg(1), Msg(2));
        Pendentes(a, b);
        _publisher.PublishAsync(a, Arg.Any<CancellationToken>()).Returns(_ => throw new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(() => _processor.ProcessBatchAsync(CancellationToken.None));

        await _store.Received(1).ReleaseAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2), Arg.Any<CancellationToken>());
        await _store.DidNotReceiveWithAnyArgs().MarkFailedAsync(default, default!, default);
    }

    [Fact]
    public async Task ProcessBatch_MensagemPublicadaEmShutdown_EhMarcadaComoProcessadaMesmoAssim()
    {
        var a = Msg(1);
        Pendentes(a);
        using var cts = new CancellationTokenSource();

        await _processor.ProcessBatchAsync(cts.Token);
        await cts.CancelAsync();

        await _store.Received(1).MarkProcessedAsync(a.Id, CancellationToken.None);
    }

    [Fact]
    public async Task ProcessBatch_ErroMuitoLongo_EhTruncadoAntesDeGravar()
    {
        var a = Msg(1);
        Pendentes(a);
        _publisher.PublishAsync(a, Arg.Any<CancellationToken>()).Returns(_ => throw new InvalidOperationException(new string('x', 5000)));

        await Should.ThrowAsync<InvalidOperationException>(() => _processor.ProcessBatchAsync(CancellationToken.None));

        await _store.Received(1).MarkFailedAsync(
            a.Id, Arg.Is<string>(e => e.Length == 1000), Arg.Any<CancellationToken>());
    }
}
