using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Application.Lancamentos;

public sealed record LancamentoResponse(
    Guid Id,
    string Tipo,
    decimal Valor,
    string Descricao,
    DateOnly DataCompetencia,
    string Status,
    Guid? LancamentoOriginalId,
    DateTimeOffset CriadoEm)
{
    public static LancamentoResponse From(Lancamento lancamento) => new(
        lancamento.Id,
        lancamento.Tipo.ToString(),
        lancamento.Valor.Amount,
        lancamento.Descricao.Valor,
        lancamento.DataCompetencia,
        lancamento.Status.ToString(),
        lancamento.LancamentoOriginalId,
        lancamento.CriadoEm);
}
