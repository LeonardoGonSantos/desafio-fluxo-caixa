namespace Consolidacao.Application.SaldosDiarios;

/// <param name="SaldoDoDia">Créditos − débitos do dia.</param>
/// <param name="SaldoAcumulado">Soma dos saldos de todos os dias até esta data, inclusive.</param>
/// <param name="AtualizadoEm">Última atualização do dia; nulo se o dia não teve movimento.</param>
public sealed record SaldoDiarioResponse(
    DateOnly Data,
    decimal TotalCreditos,
    decimal TotalDebitos,
    decimal SaldoDoDia,
    decimal SaldoAcumulado,
    DateTimeOffset? AtualizadoEm);
