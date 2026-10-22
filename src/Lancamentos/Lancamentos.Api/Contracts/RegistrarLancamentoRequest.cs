using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Api.Contracts;

/// <param name="Tipo">Credito ou Debito.</param>
/// <param name="Valor">Valor positivo com até 2 casas decimais.</param>
/// <param name="Descricao">Descrição do lançamento (3 a 200 caracteres).</param>
/// <param name="DataCompetencia">Data de competência (yyyy-MM-dd); não pode ser futura.</param>
public sealed record RegistrarLancamentoRequest(
    TipoLancamento Tipo,
    decimal Valor,
    string Descricao,
    DateOnly DataCompetencia);
