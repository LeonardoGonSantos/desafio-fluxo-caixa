using FluentValidation;

namespace Lancamentos.Application.Lancamentos.ListarLancamentos;

public sealed class ListarLancamentosQueryValidator : AbstractValidator<ListarLancamentosQuery>
{
    public const int MaximoDiasPeriodo = 93;
    public const int MaximoPageSize = 100;

    public ListarLancamentosQueryValidator()
    {
        RuleFor(q => q.Ate).GreaterThanOrEqualTo(q => q.De)
            .WithMessage("A data final deve ser maior ou igual à data inicial.");
        RuleFor(q => q).Must(q => q.Ate.DayNumber - q.De.DayNumber < MaximoDiasPeriodo)
            .When(q => q.Ate >= q.De)
            .WithMessage($"O período máximo de consulta é de {MaximoDiasPeriodo} dias.");
        RuleFor(q => q.Tipo).IsInEnum().When(q => q.Tipo.HasValue);
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1).WithMessage("Page deve ser maior ou igual a 1.");
        RuleFor(q => q.PageSize).InclusiveBetween(1, MaximoPageSize)
            .WithMessage($"PageSize deve estar entre 1 e {MaximoPageSize}.");
    }
}
