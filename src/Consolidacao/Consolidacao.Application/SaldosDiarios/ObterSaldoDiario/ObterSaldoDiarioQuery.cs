using FluxoCaixa.BuildingBlocks.Cqrs;

namespace Consolidacao.Application.SaldosDiarios.ObterSaldoDiario;

public sealed record ObterSaldoDiarioQuery(DateOnly Data) : IQuery<SaldoDiarioResponse>;
