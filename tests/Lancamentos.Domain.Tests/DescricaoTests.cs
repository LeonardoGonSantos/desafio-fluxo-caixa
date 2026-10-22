using Lancamentos.Domain.Lancamentos;
using Shouldly;

namespace Lancamentos.Domain.Tests;

public class DescricaoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_VaziaOuNula_Falha(string? valor)
    {
        Descricao.Criar(valor).Error.ShouldBe(LancamentoErrors.DescricaoObrigatoria);
    }

    [Fact]
    public void Criar_CurtaDemais_Falha()
    {
        Descricao.Criar("ab").Error.ShouldBe(LancamentoErrors.DescricaoTamanhoInvalido);
    }

    [Fact]
    public void Criar_AcimaDe200Caracteres_Falha()
    {
        Descricao.Criar(new string('a', 201)).Error.ShouldBe(LancamentoErrors.DescricaoTamanhoInvalido);
    }

    [Fact]
    public void Criar_ComEspacosNasPontas_FazTrim()
    {
        Descricao.Criar("  Venda balcão  ").Value.Valor.ShouldBe("Venda balcão");
    }
}
