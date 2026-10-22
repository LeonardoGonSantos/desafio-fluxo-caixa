using FluxoCaixa.BuildingBlocks.Results;

namespace FluxoCaixa.BuildingBlocks.Domain;

/// <summary>Valor monetário em BRL, não negativo, com no máximo 2 casas decimais.</summary>
public sealed class Money : ValueObject
{
    public static readonly Error Negative = Error.Validation("money.negative", "O valor monetário não pode ser negativo.");
    public static readonly Error TooManyDecimals = Error.Validation("money.decimals", "O valor monetário deve ter no máximo 2 casas decimais.");

    public static readonly Money Zero = new(0m);

    private Money(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public static Result<Money> Create(decimal amount)
    {
        if (amount < 0)
        {
            return Negative;
        }

        if (decimal.Round(amount, 2) != amount)
        {
            return TooManyDecimals;
        }

        return new Money(amount);
    }

    /// <summary>Reconstrói um valor já validado (ex.: vindo do banco).</summary>
    public static Money FromTrusted(decimal amount) => new(amount);

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
    }

    public override string ToString() => Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}
