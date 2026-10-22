using FluentValidation;
using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;
using Lancamentos.Application.Abstractions;
using Lancamentos.Application.Behaviors;
using Lancamentos.Application.Lancamentos.RegistrarLancamento;
using Lancamentos.Domain.Lancamentos;
using NSubstitute;
using Shouldly;

namespace Lancamentos.Application.Tests;

public class DecoratorTests
{
    private static readonly RegistrarLancamentoCommand ComandoInvalido =
        new(TipoLancamento.Credito, 0m, "", default);

    private static readonly RegistrarLancamentoCommand ComandoValido =
        new(TipoLancamento.Credito, 10m, "Venda", new DateOnly(2025, 6, 10));

    private readonly ICommandHandler<RegistrarLancamentoCommand, Application.Lancamentos.LancamentoResponse> _inner =
        Substitute.For<ICommandHandler<RegistrarLancamentoCommand, Application.Lancamentos.LancamentoResponse>>();

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Validation_ComandoInvalido_NaoChamaHandlerInterno()
    {
        var decorator = new CommandValidationDecorator<RegistrarLancamentoCommand, Application.Lancamentos.LancamentoResponse>(
            _inner, [new RegistrarLancamentoCommandValidator()]);

        await decorator.Handle(ComandoInvalido, CancellationToken.None);

        await _inner.DidNotReceiveWithAnyArgs().Handle(default!, default);
    }

    [Fact]
    public async Task Validation_ComandoInvalido_RetornaErroComTodasAsMensagens()
    {
        var decorator = new CommandValidationDecorator<RegistrarLancamentoCommand, Application.Lancamentos.LancamentoResponse>(
            _inner, [new RegistrarLancamentoCommandValidator()]);

        var result = await decorator.Handle(ComandoInvalido, CancellationToken.None);

        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("Valor deve ser maior que zero.");
        result.Error.Message.ShouldContain("Descrição é obrigatória.");
        result.Error.Message.ShouldContain("Data de competência é obrigatória.");
    }

    [Fact]
    public async Task Validation_ComandoValido_DelegaParaOHandler()
    {
        _inner.Handle(ComandoValido, Arg.Any<CancellationToken>()).Returns(LancamentoErrors.DataFutura);
        var decorator = new CommandValidationDecorator<RegistrarLancamentoCommand, Application.Lancamentos.LancamentoResponse>(
            _inner, [new RegistrarLancamentoCommandValidator()]);

        var result = await decorator.Handle(ComandoValido, CancellationToken.None);

        result.Error.ShouldBe(LancamentoErrors.DataFutura);
    }

    [Fact]
    public async Task Transaction_Sucesso_FazCommit()
    {
        _inner.Handle(ComandoValido, Arg.Any<CancellationToken>()).Returns(Result.Success(Resposta()));
        var decorator = new CommandTransactionDecorator<RegistrarLancamentoCommand, Application.Lancamentos.LancamentoResponse>(_inner, _unitOfWork);

        await decorator.Handle(ComandoValido, CancellationToken.None);

        Received.InOrder(() =>
        {
            _unitOfWork.BeginAsync(Arg.Any<CancellationToken>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.DidNotReceiveWithAnyArgs().RollbackAsync(default);
    }

    [Fact]
    public async Task Transaction_ResultFailure_FazRollback()
    {
        _inner.Handle(ComandoValido, Arg.Any<CancellationToken>()).Returns(LancamentoErrors.DataFutura);
        var decorator = new CommandTransactionDecorator<RegistrarLancamentoCommand, Application.Lancamentos.LancamentoResponse>(_inner, _unitOfWork);

        await decorator.Handle(ComandoValido, CancellationToken.None);

        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceiveWithAnyArgs().CommitAsync(default);
    }

    [Fact]
    public async Task Transaction_Excecao_FazRollbackERelanca()
    {
        _inner.Handle(ComandoValido, Arg.Any<CancellationToken>()).Returns<Result<Application.Lancamentos.LancamentoResponse>>(
            _ => throw new InvalidOperationException("boom"));
        var decorator = new CommandTransactionDecorator<RegistrarLancamentoCommand, Application.Lancamentos.LancamentoResponse>(_inner, _unitOfWork);

        await Should.ThrowAsync<InvalidOperationException>(() => decorator.Handle(ComandoValido, CancellationToken.None));

        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceiveWithAnyArgs().CommitAsync(default);
    }

    private static Application.Lancamentos.LancamentoResponse Resposta() =>
        new(Guid.NewGuid(), "Credito", 10m, "Venda", new DateOnly(2025, 6, 10), "Ativo", null, DateTimeOffset.UtcNow);
}
