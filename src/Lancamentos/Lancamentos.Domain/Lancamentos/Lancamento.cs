using FluxoCaixa.BuildingBlocks.Domain;
using FluxoCaixa.BuildingBlocks.Results;
using Lancamentos.Domain.Events;

namespace Lancamentos.Domain.Lancamentos;

/// <summary>
/// Aggregate Root. Um lançamento é imutável: correções são feitas por estorno
/// (novo lançamento de tipo inverso), preservando a trilha de auditoria.
/// </summary>
public sealed class Lancamento : AggregateRoot<Guid>
{
    private static readonly TimeZoneInfo FusoNegocio = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private Lancamento(
        Guid id,
        TipoLancamento tipo,
        Money valor,
        Descricao descricao,
        DateOnly dataCompetencia,
        StatusLancamento status,
        Guid? lancamentoOriginalId,
        DateTimeOffset criadoEm) : base(id)
    {
        Tipo = tipo;
        Valor = valor;
        Descricao = descricao;
        DataCompetencia = dataCompetencia;
        Status = status;
        LancamentoOriginalId = lancamentoOriginalId;
        CriadoEm = criadoEm;
    }

    public TipoLancamento Tipo { get; }

    public Money Valor { get; }

    public Descricao Descricao { get; }

    public DateOnly DataCompetencia { get; }

    public StatusLancamento Status { get; private set; }

    /// <summary>Preenchido apenas em lançamentos de estorno.</summary>
    public Guid? LancamentoOriginalId { get; }

    public DateTimeOffset CriadoEm { get; }

    public bool EhEstorno => LancamentoOriginalId.HasValue;

    public static Result<Lancamento> Registrar(
        TipoLancamento tipo,
        decimal valor,
        string? descricao,
        DateOnly dataCompetencia,
        TimeProvider timeProvider) =>
        Criar(tipo, valor, descricao, dataCompetencia, lancamentoOriginalId: null, timeProvider);

    public Result<Lancamento> Estornar(string? motivo, TimeProvider timeProvider)
    {
        if (EhEstorno)
        {
            return LancamentoErrors.EstornoNaoEstornavel;
        }

        if (Status == StatusLancamento.Estornado)
        {
            return LancamentoErrors.JaEstornado;
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return LancamentoErrors.MotivoObrigatorio;
        }

        var hoje = DataAtual(timeProvider);
        var descricaoEstorno = $"Estorno: {motivo.Trim()}";
        if (descricaoEstorno.Length > Descricao.TamanhoMaximo)
        {
            descricaoEstorno = descricaoEstorno[..Descricao.TamanhoMaximo];
        }

        var estorno = Criar(Tipo.Inverso(), Valor.Amount, descricaoEstorno, hoje, Id, timeProvider);
        if (estorno.IsFailure)
        {
            return estorno;
        }

        Status = StatusLancamento.Estornado;
        return estorno;
    }

    public static Lancamento Rehydrate(
        Guid id,
        TipoLancamento tipo,
        decimal valor,
        string descricao,
        DateOnly dataCompetencia,
        StatusLancamento status,
        Guid? lancamentoOriginalId,
        DateTimeOffset criadoEm) =>
        new(id, tipo, Money.FromTrusted(valor), Descricao.FromTrusted(descricao),
            dataCompetencia, status, lancamentoOriginalId, criadoEm);

    private static Result<Lancamento> Criar(
        TipoLancamento tipo,
        decimal valor,
        string? descricao,
        DateOnly dataCompetencia,
        Guid? lancamentoOriginalId,
        TimeProvider timeProvider)
    {
        if (!Enum.IsDefined(tipo))
        {
            return LancamentoErrors.TipoInvalido;
        }

        if (valor <= 0)
        {
            return LancamentoErrors.ValorInvalido;
        }

        var money = Money.Create(valor);
        if (money.IsFailure)
        {
            return money.Error;
        }

        var descricaoResult = Descricao.Criar(descricao);
        if (descricaoResult.IsFailure)
        {
            return descricaoResult.Error;
        }

        if (dataCompetencia > DataAtual(timeProvider))
        {
            return LancamentoErrors.DataFutura;
        }

        var agora = timeProvider.GetUtcNow();
        var lancamento = new Lancamento(
            Guid.NewGuid(),
            tipo,
            money.Value,
            descricaoResult.Value,
            dataCompetencia,
            StatusLancamento.Ativo,
            lancamentoOriginalId,
            agora);

        lancamento.Raise(new LancamentoRegistradoDomainEvent(
            Guid.NewGuid(), agora, lancamento.Id, tipo, money.Value.Amount, dataCompetencia));

        return lancamento;
    }

    private static DateOnly DataAtual(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), FusoNegocio).DateTime);
}
