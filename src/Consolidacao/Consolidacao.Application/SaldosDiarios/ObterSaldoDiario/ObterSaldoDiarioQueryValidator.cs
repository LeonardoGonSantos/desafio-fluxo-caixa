using Consolidacao.Domain.SaldosDiarios;
using FluentValidation;

namespace Consolidacao.Application.SaldosDiarios.ObterSaldoDiario;

public sealed class ObterSaldoDiarioQueryValidator : AbstractValidator<ObterSaldoDiarioQuery>
{
    public ObterSaldoDiarioQueryValidator(TimeProvider timeProvider)
    {
        RuleFor(q => q.Data)
            .Must(data => data <= DataNegocio.Hoje(timeProvider))
            .WithMessage(SaldoDiarioErrors.DataFutura.Message);
    }
}
