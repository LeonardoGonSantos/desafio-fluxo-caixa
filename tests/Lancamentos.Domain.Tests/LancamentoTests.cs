using FluxoCaixa.BuildingBlocks.Domain;
using Lancamentos.Domain.Events;
using Lancamentos.Domain.Lancamentos;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace Lancamentos.Domain.Tests;

public class LancamentoTests
{
    // 2025-06-10 15:00 UTC = 12:00 em São Paulo
    private static readonly DateTimeOffset Agora = new(2025, 6, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = new(2025, 6, 10);

    private readonly FakeTimeProvider _time = new(Agora);

    private Lancamento NovoCredito(decimal valor = 100m) =>
        Lancamento.Registrar(TipoLancamento.Credito, valor, "Venda balcão", Hoje, _time).Value;

    [Fact]
    public void Registrar_CreditoValido_CriaAtivoComDadosCorretos()
    {
        var result = Lancamento.Registrar(TipoLancamento.Credito, 150.75m, "Venda balcão", Hoje, _time);

        result.IsSuccess.ShouldBeTrue();
        var lancamento = result.Value;
        lancamento.Id.ShouldNotBe(Guid.Empty);
        lancamento.Tipo.ShouldBe(TipoLancamento.Credito);
        lancamento.Valor.Amount.ShouldBe(150.75m);
        lancamento.Descricao.Valor.ShouldBe("Venda balcão");
        lancamento.DataCompetencia.ShouldBe(Hoje);
        lancamento.Status.ShouldBe(StatusLancamento.Ativo);
        lancamento.EhEstorno.ShouldBeFalse();
        lancamento.CriadoEm.ShouldBe(Agora);
    }

    [Fact]
    public void Registrar_DebitoValido_CriaAtivo()
    {
        var result = Lancamento.Registrar(TipoLancamento.Debito, 20m, "Pagamento fornecedor", Hoje, _time);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Tipo.ShouldBe(TipoLancamento.Debito);
    }

    [Fact]
    public void Registrar_TipoInvalido_RetornaErroTipoInvalido()
    {
        var result = Lancamento.Registrar((TipoLancamento)99, 10m, "Qualquer coisa", Hoje, _time);

        result.Error.ShouldBe(LancamentoErrors.TipoInvalido);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Registrar_ValorZeroOuNegativo_RetornaErroValorInvalido(int valor)
    {
        var result = Lancamento.Registrar(TipoLancamento.Credito, valor, "Venda balcão", Hoje, _time);

        result.Error.ShouldBe(LancamentoErrors.ValorInvalido);
    }

    [Fact]
    public void Registrar_MaisDeDuasCasas_RetornaErroDeMoney()
    {
        var result = Lancamento.Registrar(TipoLancamento.Credito, 10.123m, "Venda balcão", Hoje, _time);

        result.Error.ShouldBe(Money.TooManyDecimals);
    }

    [Fact]
    public void Registrar_DescricaoInvalida_RetornaErroDeDescricao()
    {
        var result = Lancamento.Registrar(TipoLancamento.Credito, 10m, " ", Hoje, _time);

        result.Error.ShouldBe(LancamentoErrors.DescricaoObrigatoria);
    }

    [Fact]
    public void Registrar_DataFutura_RetornaErroDataFutura()
    {
        var result = Lancamento.Registrar(TipoLancamento.Credito, 10m, "Venda balcão", Hoje.AddDays(1), _time);

        result.Error.ShouldBe(LancamentoErrors.DataFutura);
    }

    [Fact]
    public void Registrar_DataHojeNoFusoSaoPaulo_QuandoUtcJaEhAmanha_Permite()
    {
        // 2025-06-11 01:00 UTC = 2025-06-10 22:00 em São Paulo
        _time.SetUtcNow(new DateTimeOffset(2025, 6, 11, 1, 0, 0, TimeSpan.Zero));

        var result = Lancamento.Registrar(TipoLancamento.Credito, 10m, "Venda balcão", new DateOnly(2025, 6, 10), _time);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Registrar_DataDeAmanhaNoFusoSaoPaulo_QuandoUtcJaEhAmanha_Rejeita()
    {
        _time.SetUtcNow(new DateTimeOffset(2025, 6, 11, 1, 0, 0, TimeSpan.Zero));

        var result = Lancamento.Registrar(TipoLancamento.Credito, 10m, "Venda balcão", new DateOnly(2025, 6, 11), _time);

        result.Error.ShouldBe(LancamentoErrors.DataFutura);
    }

    [Fact]
    public void Registrar_DataRetroativa_Permite()
    {
        var result = Lancamento.Registrar(TipoLancamento.Credito, 10m, "Venda balcão", Hoje.AddDays(-30), _time);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Registrar_Sucesso_DisparaLancamentoRegistradoDomainEvent()
    {
        var lancamento = NovoCredito(80m);

        var evento = lancamento.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<LancamentoRegistradoDomainEvent>();
        evento.LancamentoId.ShouldBe(lancamento.Id);
        evento.Tipo.ShouldBe(TipoLancamento.Credito);
        evento.Valor.ShouldBe(80m);
        evento.DataCompetencia.ShouldBe(Hoje);
        evento.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Registrar_Falha_NaoCriaLancamento()
    {
        var result = Lancamento.Registrar(TipoLancamento.Credito, 0, "Venda balcão", Hoje, _time);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Estornar_LancamentoAtivo_MarcaOriginalComoEstornado()
    {
        var original = NovoCredito();

        original.Estornar("Lançado por engano", _time);

        original.Status.ShouldBe(StatusLancamento.Estornado);
    }

    [Fact]
    public void Estornar_LancamentoAtivo_CriaLancamentoDeTipoInversoMesmoValorVinculadoAoOriginal()
    {
        var original = NovoCredito(250m);
        _time.Advance(TimeSpan.FromDays(2));

        var estorno = original.Estornar("Lançado por engano", _time).Value;

        estorno.Tipo.ShouldBe(TipoLancamento.Debito);
        estorno.Valor.Amount.ShouldBe(250m);
        estorno.LancamentoOriginalId.ShouldBe(original.Id);
        estorno.EhEstorno.ShouldBeTrue();
        estorno.Status.ShouldBe(StatusLancamento.Ativo);
        estorno.DataCompetencia.ShouldBe(Hoje.AddDays(2));
        estorno.Descricao.Valor.ShouldBe("Estorno: Lançado por engano");
    }

    [Fact]
    public void Estornar_LancamentoAtivo_DisparaEventoDoEstorno()
    {
        var original = NovoCredito();
        original.ClearDomainEvents();

        var estorno = original.Estornar("Lançado por engano", _time).Value;

        var evento = estorno.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<LancamentoRegistradoDomainEvent>();
        evento.Tipo.ShouldBe(TipoLancamento.Debito);
        evento.LancamentoId.ShouldBe(estorno.Id);
    }

    [Fact]
    public void Estornar_LancamentoAtivo_RegistraEventoDeAuditoriaNoOriginal()
    {
        var original = NovoCredito();
        original.ClearDomainEvents();

        var estorno = original.Estornar("  Engano de digitação ", _time).Value;

        var auditoria = original.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<LancamentoEstornadoDomainEvent>();
        auditoria.LancamentoOriginalId.ShouldBe(original.Id);
        auditoria.LancamentoEstornoId.ShouldBe(estorno.Id);
        auditoria.Motivo.ShouldBe("Engano de digitação");
    }

    [Fact]
    public void Estornar_LancamentoJaEstornado_RetornaErroConflito()
    {
        var original = NovoCredito();
        original.Estornar("Primeiro", _time);

        var result = original.Estornar("Segundo", _time);

        result.Error.ShouldBe(LancamentoErrors.JaEstornado);
    }

    [Fact]
    public void Estornar_UmEstorno_RetornaErroConflito()
    {
        var estorno = NovoCredito().Estornar("Engano", _time).Value;

        var result = estorno.Estornar("Engano do estorno", _time);

        result.Error.ShouldBe(LancamentoErrors.EstornoNaoEstornavel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Estornar_SemMotivo_RetornaErroValidacaoENaoAlteraOriginal(string? motivo)
    {
        var original = NovoCredito();

        var result = original.Estornar(motivo, _time);

        result.Error.ShouldBe(LancamentoErrors.MotivoObrigatorio);
        original.Status.ShouldBe(StatusLancamento.Ativo);
    }

    [Fact]
    public void Estornar_MotivoMuitoLongo_RetornaErroENaoAlteraOriginal()
    {
        var original = NovoCredito();
        original.ClearDomainEvents();

        var result = original.Estornar(new string('x', Lancamento.MotivoEstornoTamanhoMaximo + 1), _time);

        result.Error.ShouldBe(LancamentoErrors.MotivoMuitoLongo);
        original.Status.ShouldBe(StatusLancamento.Ativo);
        original.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Estornar_MotivoNoLimiteExato_Aceita()
    {
        var result = NovoCredito().Estornar(new string('x', Lancamento.MotivoEstornoTamanhoMaximo), _time);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Descricao.Valor.Length.ShouldBe(Descricao.TamanhoMaximo);
    }
}
