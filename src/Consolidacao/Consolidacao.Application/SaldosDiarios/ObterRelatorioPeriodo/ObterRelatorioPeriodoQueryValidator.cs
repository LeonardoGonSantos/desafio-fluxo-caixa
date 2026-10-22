using Consolidacao.Domain.SaldosDiarios;
using FluentValidation;

namespace Consolidacao.Application.SaldosDiarios.ObterRelatorioPeriodo;

public sealed class ObterRelatorioPeriodoQueryValidator : AbstractValidator<ObterRelatorioPeriodoQuery>
{
    public const int MaximoDias = 93;

    public ObterRelatorioPeriodoQueryValidator(TimeProvider timeProvider)
    {
        RuleFor(q => q.Ate).GreaterThanOrEqualTo(q => q.De).WithMessage(SaldoDiarioErrors.PeriodoInvalido.Message);
        RuleFor(q => q).Must(q => q.Ate.DayNumber - q.De.DayNumber < MaximoDias)
            .When(q => q.Ate >= q.De)
            .WithMessage(SaldoDiarioErrors.PeriodoMuitoLongo.Message);
        RuleFor(q => q.Ate).Must(ate => ate <= DataNegocio.Hoje(timeProvider))
            .WithMessage(SaldoDiarioErrors.DataFutura.Message);
    }
}
