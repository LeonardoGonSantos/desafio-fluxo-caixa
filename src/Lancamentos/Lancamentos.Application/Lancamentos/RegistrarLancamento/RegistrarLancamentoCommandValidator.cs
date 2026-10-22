using FluentValidation;

namespace Lancamentos.Application.Lancamentos.RegistrarLancamento;

/// <summary>
/// Valida apenas a FORMA da entrada (nulo, enum fora da faixa, data não informada).
/// Regras de negócio (valor positivo, tamanho da descrição, data futura) vivem no agregado,
/// para existirem em um único lugar.
/// </summary>
public sealed class RegistrarLancamentoCommandValidator : AbstractValidator<RegistrarLancamentoCommand>
{
    public RegistrarLancamentoCommandValidator()
    {
        RuleFor(c => c.Tipo).IsInEnum().WithMessage("Tipo deve ser Credito ou Debito.");
        RuleFor(c => c.Descricao).NotNull().WithMessage("Descrição é obrigatória.");
        RuleFor(c => c.DataCompetencia).NotEqual(default(DateOnly)).WithMessage("Data de competência é obrigatória.");
    }
}
