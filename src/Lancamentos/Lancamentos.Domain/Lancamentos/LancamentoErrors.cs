using FluxoCaixa.BuildingBlocks.Results;

namespace Lancamentos.Domain.Lancamentos;

public static class LancamentoErrors
{
    public static readonly Error TipoInvalido =
        Error.Validation("lancamento.tipo_invalido", "O tipo do lançamento deve ser Crédito ou Débito.");

    public static readonly Error ValorInvalido =
        Error.Validation("lancamento.valor_invalido", "O valor do lançamento deve ser maior que zero.");

    public static readonly Error DataFutura =
        Error.Validation("lancamento.data_futura", "A data de competência não pode ser futura.");

    public static readonly Error DescricaoObrigatoria =
        Error.Validation("lancamento.descricao_obrigatoria", "A descrição é obrigatória.");

    public static readonly Error DescricaoTamanhoInvalido =
        Error.Validation("lancamento.descricao_tamanho", "A descrição deve ter entre 3 e 200 caracteres.");

    public static readonly Error MotivoObrigatorio =
        Error.Validation("lancamento.motivo_obrigatorio", "O motivo do estorno é obrigatório.");

    public static readonly Error NaoEncontrado =
        Error.NotFound("lancamento.nao_encontrado", "Lançamento não encontrado.");

    public static readonly Error JaEstornado =
        Error.Conflict("lancamento.ja_estornado", "O lançamento já foi estornado.");

    public static readonly Error EstornoNaoEstornavel =
        Error.Conflict("lancamento.estorno_nao_estornavel", "Um lançamento de estorno não pode ser estornado.");
}
