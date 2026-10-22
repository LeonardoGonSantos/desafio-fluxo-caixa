using FluentValidation;

namespace Lancamentos.Application.Lancamentos.EstornarLancamento;

public sealed class EstornarLancamentoCommandValidator : AbstractValidator<EstornarLancamentoCommand>
{
    public EstornarLancamentoCommandValidator()
    {
        RuleFor(c => c.LancamentoId).NotEmpty().WithMessage("Identificador do lançamento é obrigatório.");
        RuleFor(c => c.Motivo).NotEmpty().WithMessage("Motivo do estorno é obrigatório.");
    }
}
