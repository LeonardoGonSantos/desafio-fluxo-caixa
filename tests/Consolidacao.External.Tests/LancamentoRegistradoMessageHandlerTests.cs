using System.Text;
using Consolidacao.Application.Abstractions;
using Consolidacao.Application.SaldosDiarios.AplicarLancamento;
using Consolidacao.Domain.SaldosDiarios;
using Consolidacao.External.Messaging;
using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Consolidacao.External.Tests;

public class LancamentoRegistradoMessageHandlerTests
{
    private const string Tipo = "lancamento.registrado.v1";

    private readonly ICommandHandler<AplicarLancamentoCommand, AplicarLancamentoResponse> _command =
        Substitute.For<ICommandHandler<AplicarLancamentoCommand, AplicarLancamentoResponse>>();

    private readonly ISaldoCache _cache = Substitute.For<ISaldoCache>();
    private readonly LancamentoRegistradoMessageHandler _handler;

    public LancamentoRegistradoMessageHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _command);
        services.AddScoped(_ => _cache);
        _handler = new LancamentoRegistradoMessageHandler(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LancamentoRegistradoMessageHandler>.Instance);
    }

    private static ReadOnlyMemory<byte> Json(string tipo = "Credito", decimal valor = 10.5m, Guid? eventId = null) =>
        Encoding.UTF8.GetBytes(
            $$"""{"eventId":"{{eventId ?? Guid.NewGuid()}}","occurredAt":"2025-06-10T15:00:00+00:00","lancamentoId":"{{Guid.NewGuid()}}","tipo":"{{tipo}}","valor":{{valor.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"dataCompetencia":"2025-06-10"}""");

    private void ComandoRetorna(Result<AplicarLancamentoResponse> resultado) =>
        _command.Handle(Arg.Any<AplicarLancamentoCommand>(), Arg.Any<CancellationToken>()).Returns(resultado);

    [Fact]
    public async Task EventoNovo_AplicaOComandoComOsDadosDaMensagem_InvalidaOCache_EConfirma()
    {
        var eventId = Guid.NewGuid();
        ComandoRetorna(new AplicarLancamentoResponse(true));

        var outcome = await _handler.HandleAsync(Json("Debito", 42.25m, eventId), Tipo, CancellationToken.None);

        outcome.ShouldBe(MessageOutcome.Ack);
        await _command.Received(1).Handle(
            new AplicarLancamentoCommand(eventId, TipoMovimento.Debito, 42.25m, new DateOnly(2025, 6, 10)),
            Arg.Any<CancellationToken>());
        await _cache.Received(1).InvalidateAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duplicata_ConfirmaSemInvalidarOCache()
    {
        ComandoRetorna(new AplicarLancamentoResponse(false));

        var outcome = await _handler.HandleAsync(Json(), Tipo, CancellationToken.None);

        outcome.ShouldBe(MessageOutcome.Ack);
        await _cache.DidNotReceiveWithAnyArgs().InvalidateAllAsync(default);
    }

    [Fact]
    public async Task JsonInvalido_Rejeita_SemChamarOComando()
    {
        var outcome = await _handler.HandleAsync(Encoding.UTF8.GetBytes("não é json"), Tipo, CancellationToken.None);

        outcome.ShouldBe(MessageOutcome.Reject);
        await _command.DidNotReceiveWithAnyArgs().Handle(default!, default);
    }

    [Fact]
    public async Task CorpoNulo_Rejeita()
    {
        (await _handler.HandleAsync(Encoding.UTF8.GetBytes("null"), Tipo, CancellationToken.None)).ShouldBe(MessageOutcome.Reject);
    }

    [Fact]
    public async Task TipoDeMensagemDesconhecido_Rejeita()
    {
        (await _handler.HandleAsync(Json(), "outro.evento.v9", CancellationToken.None)).ShouldBe(MessageOutcome.Reject);
        (await _handler.HandleAsync(Json(), null, CancellationToken.None)).ShouldBe(MessageOutcome.Reject);
    }

    [Fact]
    public async Task EventIdVazio_Rejeita()
    {
        (await _handler.HandleAsync(Json(eventId: Guid.Empty), Tipo, CancellationToken.None)).ShouldBe(MessageOutcome.Reject);
    }

    [Theory]
    [InlineData("Transferencia")]
    [InlineData("99")]
    [InlineData("")]
    public async Task TipoDeMovimentoInvalido_Rejeita(string tipo)
    {
        (await _handler.HandleAsync(Json(tipo), Tipo, CancellationToken.None)).ShouldBe(MessageOutcome.Reject);
    }

    [Fact]
    public async Task TipoEmMinusculas_EAceito()
    {
        ComandoRetorna(new AplicarLancamentoResponse(true));

        (await _handler.HandleAsync(Json("credito"), Tipo, CancellationToken.None)).ShouldBe(MessageOutcome.Ack);
    }

    [Fact]
    public async Task ErroDeDominio_Rejeita_SemInvalidarOCache()
    {
        ComandoRetorna(SaldoDiarioErrors.MovimentoInvalido);

        var outcome = await _handler.HandleAsync(Json(valor: 5m), Tipo, CancellationToken.None);

        outcome.ShouldBe(MessageOutcome.Reject);
        await _cache.DidNotReceiveWithAnyArgs().InvalidateAllAsync(default);
    }

    [Fact]
    public async Task ExcecaoDoComando_EFalhaTransitoria_Retry()
    {
        _command.Handle(Arg.Any<AplicarLancamentoCommand>(), Arg.Any<CancellationToken>())
            .Returns<Result<AplicarLancamentoResponse>>(_ => throw new TimeoutException("banco fora"));

        var outcome = await _handler.HandleAsync(Json(), Tipo, CancellationToken.None);

        outcome.ShouldBe(MessageOutcome.Retry);
        await _cache.DidNotReceiveWithAnyArgs().InvalidateAllAsync(default);
    }
}
