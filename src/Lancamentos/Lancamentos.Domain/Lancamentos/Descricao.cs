using FluxoCaixa.BuildingBlocks.Domain;
using FluxoCaixa.BuildingBlocks.Results;

namespace Lancamentos.Domain.Lancamentos;

public sealed class Descricao : ValueObject
{
    public const int TamanhoMinimo = 3;
    public const int TamanhoMaximo = 200;

    private Descricao(string valor) => Valor = valor;

    public string Valor { get; }

    public static Result<Descricao> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return LancamentoErrors.DescricaoObrigatoria;
        }

        var normalizado = valor.Trim();
        if (normalizado.Length is < TamanhoMinimo or > TamanhoMaximo)
        {
            return LancamentoErrors.DescricaoTamanhoInvalido;
        }

        return new Descricao(normalizado);
    }

    public static Descricao FromTrusted(string valor) => new(valor);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Valor;
    }

    public override string ToString() => Valor;
}
