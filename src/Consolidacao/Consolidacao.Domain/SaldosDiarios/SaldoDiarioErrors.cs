using FluxoCaixa.BuildingBlocks.Results;

namespace Consolidacao.Domain.SaldosDiarios;

public static class SaldoDiarioErrors
{
    public static readonly Error DataFutura =
        Error.Validation("saldo.data_futura", "A data da consulta não pode ser futura.");

    public static readonly Error PeriodoInvalido =
        Error.Validation("saldo.periodo_invalido", "A data final deve ser maior ou igual à data inicial.");

    public static readonly Error PeriodoMuitoLongo =
        Error.Validation("saldo.periodo_longo", "O período máximo de consulta é de 93 dias.");

    public static readonly Error MovimentoInvalido =
        Error.Validation("saldo.movimento_invalido", "Tipo ou valor do movimento inválido.");
}
