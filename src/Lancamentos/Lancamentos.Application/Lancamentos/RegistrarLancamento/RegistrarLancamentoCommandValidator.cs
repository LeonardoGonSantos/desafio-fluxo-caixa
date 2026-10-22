using FluentValidation;

namespace Lancamentos.Application.Lancamentos.RegistrarLancamento;

/// <summary>Valida a forma da entrada. As regras de negócio vivem no domínio.</summary>
public sealed class RegistrarLancamentoCommandValidator : AbstractValidator<RegistrarLancamentoCommand>
{
    public RegistrarLancamentoCommandValidator()
    {
        RuleFor(c => c.Tipo).IsInEnum().WithMessage("Tipo deve ser Credito ou Debito.");
        RuleFor(c => c.Valor).GreaterThan(0).WithMessage("Valor deve ser maior que zero.");
        RuleFor(c => c.Descricao).NotEmpty().WithMessage("Descrição é obrigatória.");
        RuleFor(c => c.DataCompetencia).NotEqual(default(DateOnly)).WithMessage("Data de competência é obrigatória.");
    }
}
