using FluentValidation;
using FluxoCaixa.BuildingBlocks.Application.Abstractions;
using FluxoCaixa.BuildingBlocks.Application.Behaviors;
using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;
using NSubstitute;
using Shouldly;

namespace FluxoCaixa.BuildingBlocks.Application.Tests;

public sealed record FakeCommand(string Nome, int Quantidade) : ICommand<string>;

public sealed class FakeCommandValidator : AbstractValidator<FakeCommand>
{
    public FakeCommandValidator()
    {
        RuleFor(c => c.Nome).NotEmpty().WithMessage("Nome é obrigatório.");
        RuleFor(c => c.Quantidade).GreaterThan(0).WithMessage("Quantidade deve ser positiva.");
    }
}

public class DecoratorTests
{
    private static readonly FakeCommand Invalido = new("", 0);
    private static readonly FakeCommand Valido = new("abc", 1);
    private static readonly Error ErroDeNegocio = Error.Conflict("fake.erro", "falha de negócio");

    private readonly ICommandHandler<FakeCommand, string> _inner = Substitute.For<ICommandHandler<FakeCommand, string>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private CommandValidationDecorator<FakeCommand, string> Validation() => new(_inner, [new FakeCommandValidator()]);

    private CommandTransactionDecorator<FakeCommand, string> Transaction() => new(_inner, _unitOfWork);

    [Fact]
    public async Task Validation_ComandoInvalido_NaoChamaHandlerInterno()
    {
        await Validation().Handle(Invalido, CancellationToken.None);

        await _inner.DidNotReceiveWithAnyArgs().Handle(default!, default);
    }

    [Fact]
    public async Task Validation_ComandoInvalido_RetornaErroComTodasAsMensagens()
    {
        var result = await Validation().Handle(Invalido, CancellationToken.None);

        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldContain("Nome é obrigatório.");
        result.Error.Message.ShouldContain("Quantidade deve ser positiva.");
    }

    [Fact]
    public async Task Validation_ComandoValido_DelegaParaOHandlerERetornaOResultado()
    {
        _inner.Handle(Valido, Arg.Any<CancellationToken>()).Returns(ErroDeNegocio);

        var result = await Validation().Handle(Valido, CancellationToken.None);

        result.Error.ShouldBe(ErroDeNegocio);
    }

    [Fact]
    public async Task Validation_SemValidators_DelegaParaOHandler()
    {
        _inner.Handle(Invalido, Arg.Any<CancellationToken>()).Returns(Result.Success("ok"));
        var decorator = new CommandValidationDecorator<FakeCommand, string>(_inner, []);

        var result = await decorator.Handle(Invalido, CancellationToken.None);

        result.Value.ShouldBe("ok");
    }

    [Fact]
    public async Task Transaction_Sucesso_FazBeginECommitNessaOrdem()
    {
        _inner.Handle(Valido, Arg.Any<CancellationToken>()).Returns(Result.Success("ok"));

        await Transaction().Handle(Valido, CancellationToken.None);

        Received.InOrder(() =>
        {
            _unitOfWork.BeginAsync(Arg.Any<CancellationToken>());
            _unitOfWork.CommitAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.DidNotReceiveWithAnyArgs().RollbackAsync(default);
    }

    [Fact]
    public async Task Transaction_ResultFailure_FazRollbackENaoCommit()
    {
        _inner.Handle(Valido, Arg.Any<CancellationToken>()).Returns(ErroDeNegocio);

        await Transaction().Handle(Valido, CancellationToken.None);

        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceiveWithAnyArgs().CommitAsync(default);
    }

    [Fact]
    public async Task Transaction_Excecao_FazRollbackERelanca()
    {
        _inner.Handle(Valido, Arg.Any<CancellationToken>())
            .Returns<Result<string>>(_ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => Transaction().Handle(Valido, CancellationToken.None));

        await _unitOfWork.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceiveWithAnyArgs().CommitAsync(default);
    }
}

public class ValidationDetailsTests
{
    [Fact]
    public async Task Validation_ComandoInvalido_AgrupaMensagensPorCampoEmCamelCase()
    {
        var inner = Substitute.For<ICommandHandler<FakeCommand, string>>();
        var decorator = new CommandValidationDecorator<FakeCommand, string>(inner, [new FakeCommandValidator()]);

        var result = await decorator.Handle(new FakeCommand("", 0), CancellationToken.None);

        var details = result.Error.Details.ShouldNotBeNull();
        details.Keys.ShouldBe(["nome", "quantidade"], ignoreOrder: true);
        details["nome"].ShouldBe(["Nome é obrigatório."]);
        details["quantidade"].ShouldBe(["Quantidade deve ser positiva."]);
        result.Error.Code.ShouldBe("validation.failed");
    }
}
