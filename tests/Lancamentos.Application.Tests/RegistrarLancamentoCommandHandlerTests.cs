using Lancamentos.Application.Abstractions;
using Lancamentos.Application.IntegrationEvents;
using Lancamentos.Application.Lancamentos.RegistrarLancamento;
using Lancamentos.Domain.Lancamentos;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Lancamentos.Application.Tests;

public class RegistrarLancamentoCommandHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2025, 6, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2025, 6, 10);

    private readonly ILancamentoRepository _repository = Substitute.For<ILancamentoRepository>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();
    private readonly RegistrarLancamentoCommandHandler _handler;

    public RegistrarLancamentoCommandHandlerTests() =>
        _handler = new RegistrarLancamentoCommandHandler(_repository, _outbox, new FakeTimeProvider(Agora));

    private static RegistrarLancamentoCommand ComandoValido() =>
        new(TipoLancamento.Credito, 100m, "Venda balcão", Hoje);

    [Fact]
    public async Task Handle_ComandoValido_PersisteLancamento()
    {
        await _handler.Handle(ComandoValido(), CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<Lancamento>(l => l.Valor.Amount == 100m && l.Tipo == TipoLancamento.Credito),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ComandoValido_EnfileiraIntegrationEventNoOutbox()
    {
        await _handler.Handle(ComandoValido(), CancellationToken.None);

        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<LancamentoRegistradoIntegrationEvent>(e =>
                e.Tipo == "Credito" && e.Valor == 100m && e.DataCompetencia == Hoje),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ComandoValido_RetornaResponseComId()
    {
        var result = await _handler.Handle(ComandoValido(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldNotBe(Guid.Empty);
        result.Value.Status.ShouldBe("Ativo");
    }

    [Fact]
    public async Task Handle_RegraDeDominioViolada_NaoPersisteNemEnfileira()
    {
        var comando = ComandoValido() with { DataCompetencia = Hoje.AddDays(5) };

        var result = await _handler.Handle(comando, CancellationToken.None);

        result.Error.ShouldBe(LancamentoErrors.DataFutura);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _outbox.DidNotReceiveWithAnyArgs().EnqueueAsync<LancamentoRegistradoIntegrationEvent>(default!, default);
    }
}
