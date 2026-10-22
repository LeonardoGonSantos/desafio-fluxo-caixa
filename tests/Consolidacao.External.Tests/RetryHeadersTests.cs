using System.Text;
using Consolidacao.External.Messaging;
using Shouldly;

namespace Consolidacao.External.Tests;

public class RetryHeadersTests
{
    [Fact]
    public void SemHeaders_PrimeiraEntrega_Zero() => RetryHeaders.ReadRetryCount(null).ShouldBe(0);

    [Fact]
    public void HeaderAusente_Zero() =>
        RetryHeaders.ReadRetryCount(new Dictionary<string, object?> { ["outro"] = 1 }).ShouldBe(0);

    [Theory]
    [MemberData(nameof(Valores))]
    public void LeOContadorEmQualquerRepresentacaoQueOBrokerDevolva(object valor, int esperado) =>
        RetryHeaders.ReadRetryCount(new Dictionary<string, object?> { [RetryHeaders.RetryCount] = valor }).ShouldBe(esperado);

    public static TheoryData<object, int> Valores => new()
    {
        { 3, 3 },
        { 4L, 4 },
        { (short)2, 2 },
        { (byte)1, 1 },
        { Encoding.UTF8.GetBytes("5"), 5 },
        { "6", 6 },
    };

    [Fact]
    public void ValorInesperado_TrataComoZero() =>
        RetryHeaders.ReadRetryCount(new Dictionary<string, object?> { [RetryHeaders.RetryCount] = new object() }).ShouldBe(0);
}
