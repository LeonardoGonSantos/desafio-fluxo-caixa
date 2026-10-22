using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;
using Lancamentos.Application.Abstractions;
using Lancamentos.Application.IntegrationEvents;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Application.Lancamentos.RegistrarLancamento;

public sealed class RegistrarLancamentoCommandHandler(
    ILancamentoRepository repository,
    IOutbox outbox,
    TimeProvider timeProvider) : ICommandHandler<RegistrarLancamentoCommand, LancamentoResponse>
{
    public async Task<Result<LancamentoResponse>> Handle(
        RegistrarLancamentoCommand command, CancellationToken cancellationToken)
    {
        var result = Lancamento.Registrar(
            command.Tipo, command.Valor, command.Descricao, command.DataCompetencia, timeProvider);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var lancamento = result.Value;
        await repository.AddAsync(lancamento, cancellationToken);
        await outbox.EnqueueDomainEventsAsync(lancamento, cancellationToken);

        return LancamentoResponse.From(lancamento);
    }
}
