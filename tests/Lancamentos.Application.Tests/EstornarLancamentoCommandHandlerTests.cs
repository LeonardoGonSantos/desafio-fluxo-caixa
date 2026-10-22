using Lancamentos.Application.Abstractions;
using Lancamentos.Application.IntegrationEvents;
using Lancamentos.Application.Lancamentos.EstornarLancamento;
using Lancamentos.Domain.Lancamentos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Lancamentos.Application.Tests;

public class EstornarLancamentoCommandHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2025, 6, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2025, 6, 10);

    private readonly FakeTimeProvider _time = new(Agora);
    private readonly ILancamentoRepository _repository = Substitute.For<ILancamentoRepository>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly EstornarLancamentoCommandHandler _handler;

    public EstornarLancamentoCommandHandlerTests() =>
        _handler = new EstornarLancamentoCommandHandler(
            _repository, _outbox, _time, NullLogger<EstornarLancamentoCommandHandler>.Instance);

    private Lancamento Existente()
    {
        var lancamento = Lancamento.Registrar(TipoLancamento.Credito, 100m, "Venda balcão", Hoje, _time).Value;
        lancamento.ClearDomainEvents();
        _repository.GetByIdForUpdateAsync(lancamento.Id, Arg.Any<CancellationToken>()).Returns(lancamento);
        return lancamento;
    }

    [Fact]
    public async Task Handle_LancamentoInexistente_RetornaNotFound()
    {
        var result = await _handler.Handle(new EstornarLancamentoCommand(Guid.NewGuid(), "Engano"), CancellationToken.None);

        result.Error.ShouldBe(LancamentoErrors.NaoEncontrado);
    }

    [Fact]
    public async Task Handle_LancamentoJaEstornado_RetornaConflict()
    {
        var lancamento = Existente();
        lancamento.Estornar("Primeiro", _time);

        var result = await _handler.Handle(new EstornarLancamentoCommand(lancamento.Id, "Segundo"), CancellationToken.None);

        result.Error.ShouldBe(LancamentoErrors.JaEstornado);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Handle_Valido_AtualizaOriginalEInsereEstorno()
    {
        var original = Existente();

        var result = await _handler.Handle(new EstornarLancamentoCommand(original.Id, "Engano"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.LancamentoOriginalId.ShouldBe(original.Id);
        result.Value.Tipo.ShouldBe("Debito");
        await _repository.Received(1).UpdateStatusAsync(
            Arg.Is<Lancamento>(l => l.Id == original.Id && l.Status == StatusLancamento.Estornado),
            Arg.Any<CancellationToken>());
        await _repository.Received(1).AddAsync(
            Arg.Is<Lancamento>(l => l.EhEstorno && l.LancamentoOriginalId == original.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Valido_EnfileiraEventoDoEstorno()
    {
        var original = Existente();

        await _handler.Handle(new EstornarLancamentoCommand(original.Id, "Engano"), CancellationToken.None);

        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<LancamentoRegistradoIntegrationEvent>(e => e.Tipo == "Debito" && e.Valor == 100m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Valido_EventoDeAuditoriaNaoViraMensagemDeIntegracao()
    {
        var original = Existente();

        await _handler.Handle(new EstornarLancamentoCommand(original.Id, "Engano"), CancellationToken.None);

        // Só o evento do lançamento de estorno é publicado; o de auditoria do original não.
        await _outbox.Received(1).EnqueueAsync(
            Arg.Any<LancamentoRegistradoIntegrationEvent>(), Arg.Any<CancellationToken>());
        original.DomainEvents.ShouldBeEmpty();
    }
}
