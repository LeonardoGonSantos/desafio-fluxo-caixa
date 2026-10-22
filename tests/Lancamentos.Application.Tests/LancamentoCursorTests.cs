using Lancamentos.Application.Common;
using Shouldly;

namespace Lancamentos.Application.Tests;

public class LancamentoCursorTests
{
    [Fact]
    public void EncodeDecode_RoundTrip_PreservaTodosOsCampos()
    {
        var original = new LancamentoCursor(new DateOnly(2025, 6, 10), new DateTimeOffset(2025, 6, 10, 15, 30, 45, 123, TimeSpan.Zero).AddTicks(4560), Guid.NewGuid());

        LancamentoCursor.TryDecode(original.Encode(), out var decodificado).ShouldBeTrue();

        decodificado.ShouldBe(original);
    }

    [Fact]
    public void Encode_ProduzTextoSeguroParaUrl()
    {
        var texto = new LancamentoCursor(new DateOnly(2025, 1, 1), DateTimeOffset.UtcNow, Guid.NewGuid()).Encode();

        texto.ShouldNotContain("+");
        texto.ShouldNotContain("/");
        texto.ShouldNotContain("=");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("%%%")]
    [InlineData("YWJj")]                                  // "abc": sem os 3 campos
    [InlineData("MjAyNS0wNi0xMHwxfG5hby1ndWlk")]            // guid inválido
    public void TryDecode_EntradasInvalidas_RetornaFalseSemLancar(string? valor)
    {
        LancamentoCursor.TryDecode(valor, out _).ShouldBeFalse();
    }
}
