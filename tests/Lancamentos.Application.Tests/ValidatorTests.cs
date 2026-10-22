using Lancamentos.Application.Common;
using Lancamentos.Application.Lancamentos.ListarLancamentos;
using Lancamentos.Application.Lancamentos.RegistrarLancamento;
using Lancamentos.Domain.Lancamentos;
using Shouldly;

namespace Lancamentos.Application.Tests;

public class ValidatorTests
{
    private static readonly DateOnly Hoje = new(2025, 6, 10);
    private readonly RegistrarLancamentoCommandValidator _registrar = new();
    private readonly ListarLancamentosQueryValidator _listar = new();

    [Fact]
    public void Registrar_TipoForaDoEnum_Falha() =>
        _registrar.Validate(new RegistrarLancamentoCommand((TipoLancamento)99, 10m, "Venda", Hoje)).IsValid.ShouldBeFalse();

    [Fact]
    public void Registrar_DescricaoNula_Falha() =>
        _registrar.Validate(new RegistrarLancamentoCommand(TipoLancamento.Credito, 10m, null!, Hoje)).IsValid.ShouldBeFalse();

    [Fact]
    public void Registrar_DataPadrao_Falha() =>
        _registrar.Validate(new RegistrarLancamentoCommand(TipoLancamento.Credito, 10m, "Venda", default)).IsValid.ShouldBeFalse();

    [Fact]
    public void Registrar_ComandoValido_Passa() =>
        _registrar.Validate(new RegistrarLancamentoCommand(TipoLancamento.Credito, 10m, "Venda", Hoje)).IsValid.ShouldBeTrue();

    [Fact]
    public void Listar_DeMaiorQueAte_Falha() =>
        _listar.Validate(new ListarLancamentosQuery(Hoje, Hoje.AddDays(-1), null)).IsValid.ShouldBeFalse();

    [Fact]
    public void Listar_IntervaloMaiorQue93Dias_Falha() =>
        _listar.Validate(new ListarLancamentosQuery(Hoje, Hoje.AddDays(93), null)).IsValid.ShouldBeFalse();

    [Fact]
    public void Listar_Exatamente93Dias_Passa() =>
        _listar.Validate(new ListarLancamentosQuery(Hoje, Hoje.AddDays(92), null)).IsValid.ShouldBeTrue();

    [Fact]
    public void Listar_PageSizeMaiorQue100_Falha() =>
        _listar.Validate(new ListarLancamentosQuery(Hoje, Hoje, null, 101)).IsValid.ShouldBeFalse();

    [Fact]
    public void Listar_PageSizeZero_Falha() =>
        _listar.Validate(new ListarLancamentosQuery(Hoje, Hoje, null, 0)).IsValid.ShouldBeFalse();

    [Fact]
    public void Listar_CursorMalformado_Falha() =>
        _listar.Validate(new ListarLancamentosQuery(Hoje, Hoje, null, 20, "isto-nao-e-um-cursor")).IsValid.ShouldBeFalse();

    [Fact]
    public void Listar_CursorValido_Passa()
    {
        var cursor = new LancamentoCursor(Hoje, DateTimeOffset.UtcNow, Guid.NewGuid()).Encode();

        _listar.Validate(new ListarLancamentosQuery(Hoje, Hoje, null, 20, cursor)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Listar_SemCursor_PrimeiraPagina_Passa() =>
        _listar.Validate(new ListarLancamentosQuery(Hoje, Hoje, null)).IsValid.ShouldBeTrue();
}
