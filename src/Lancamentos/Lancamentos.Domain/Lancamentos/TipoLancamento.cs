namespace Lancamentos.Domain.Lancamentos;

public enum TipoLancamento
{
    Credito = 1,
    Debito = 2,
}

public static class TipoLancamentoExtensions
{
    public static TipoLancamento Inverso(this TipoLancamento tipo) =>
        tipo == TipoLancamento.Credito ? TipoLancamento.Debito : TipoLancamento.Credito;
}
