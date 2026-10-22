using FluxoCaixa.BuildingBlocks.Results;
using Shouldly;

namespace FluxoCaixa.BuildingBlocks.Tests;

public class ResultTests
{
    private static readonly Error SampleError = Error.Validation("x", "erro");

    [Fact]
    public void Success_ExpoeValor()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBe(Error.None);
    }

    [Fact]
    public void Failure_ExpoeErro()
    {
        Result<int> result = SampleError;

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SampleError);
    }

    [Fact]
    public void Failure_AcessarValor_Lanca()
    {
        Result<int> result = SampleError;

        Should.Throw<InvalidOperationException>(() => result.Value);
    }
}
