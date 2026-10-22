namespace Lancamentos.Api.Contracts;

/// <param name="Motivo">Motivo do estorno (registrado na descrição do lançamento de estorno).</param>
public sealed record EstornarLancamentoRequest(string Motivo);
