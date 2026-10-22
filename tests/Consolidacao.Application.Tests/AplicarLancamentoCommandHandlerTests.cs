using Consolidacao.Application.Abstractions;
using Consolidacao.Application.SaldosDiarios.AplicarLancamento;
using Consolidacao.Domain.SaldosDiarios;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Consolidacao.Application.Tests;

public class AplicarLancamentoCommandHandlerTests
{
    private static readonly DateOnly Dia = new(2025, 6, 10);
    private static readonly DateTimeOffset Agora = new(2025, 6, 10, 15, 0, 0, TimeSpan.Zero);

    private readonly IProcessedEventStore _processed = Substitute.For<IProcessedEventStore>();
    private readonly ISaldoDiarioRepository _repository = Substitute.For<ISaldoDiarioRepository>();
    private readonly AplicarLancamentoCommandHandler _handler;

    public AplicarLancamentoCommandHandlerTests()
    {
        _handler = new AplicarLancamentoCommandHandler(_processed, _repository, new FakeTimeProvider(Agora));
        _repository.GetForUpdateOrCreateAsync(Dia, Agora, Arg.Any<CancellationToken>())
            .Returns(_ => SaldoDiario.Abrir(Dia, Agora));
    }

    private static AplicarLancamentoCommand Comando(TipoMovimento tipo = TipoMovimento.Credito, decimal valor = 100m) =>
        new(Guid.NewGuid(), tipo, valor, Dia);

    [Fact]
    public async Task Handle_EventoNovo_AplicaNoSaldoDoDiaEPersiste()
    {
        _processed.TryRegisterAsync(default, default, default).ReturnsForAnyArgs(true);

        var result = await _handler.Handle(Comando(TipoMovimento.Credito, 100m), CancellationToken.None);

        result.Value.Aplicado.ShouldBeTrue();
        await _repository.Received(1).SaveAsync(
            Arg.Is<SaldoDiario>(s => s.Data == Dia && s.TotalCreditos.Amount == 100m && s.TotalDebitos.Amount == 0m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EventoNovoDeDebito_SomaEmDebitos()
    {
        _processed.TryRegisterAsync(default, default, default).ReturnsForAnyArgs(true);

        await _handler.Handle(Comando(TipoMovimento.Debito, 40m), CancellationToken.None);

        await _repository.Received(1).SaveAsync(
            Arg.Is<SaldoDiario>(s => s.TotalDebitos.Amount == 40m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EventoJaProcessado_NaoAplicaNovamente()
    {
        _processed.TryRegisterAsync(default, default, default).ReturnsForAnyArgs(false);

        await _handler.Handle(Comando(), CancellationToken.None);

        await _repository.DidNotReceiveWithAnyArgs().GetForUpdateOrCreateAsync(default, default, default);
        await _repository.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task Handle_EventoJaProcessado_RetornaSucessoParaOConsumerConfirmar()
    {
        _processed.TryRegisterAsync(default, default, default).ReturnsForAnyArgs(false);

        var result = await _handler.Handle(Comando(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Aplicado.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_RegistraOEventoComSeuProprioId()
    {
        _processed.TryRegisterAsync(default, default, default).ReturnsForAnyArgs(true);
        var comando = Comando();

        await _handler.Handle(comando, CancellationToken.None);

        await _processed.Received(1).TryRegisterAsync(comando.EventId, Agora, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MovimentoInvalido_RetornaErroENaoPersiste()
    {
        _processed.TryRegisterAsync(default, default, default).ReturnsForAnyArgs(true);

        var result = await _handler.Handle(Comando(valor: 0m), CancellationToken.None);

        result.Error.ShouldBe(SaldoDiarioErrors.MovimentoInvalido);
        await _repository.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }
}
