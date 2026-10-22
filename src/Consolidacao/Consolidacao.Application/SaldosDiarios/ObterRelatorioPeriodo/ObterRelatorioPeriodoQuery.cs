using FluxoCaixa.BuildingBlocks.Cqrs;

namespace Consolidacao.Application.SaldosDiarios.ObterRelatorioPeriodo;

public sealed record ObterRelatorioPeriodoQuery(DateOnly De, DateOnly Ate) : IQuery<RelatorioPeriodoResponse>;

/// <summary>Um item por dia do período (dias sem movimento aparecem zerados), com o acumulado correto.</summary>
public sealed record RelatorioPeriodoResponse(
    DateOnly De,
    DateOnly Ate,
    decimal TotalCreditos,
    decimal TotalDebitos,
    decimal SaldoDoPeriodo,
    IReadOnlyList<SaldoDiarioResponse> Dias);
