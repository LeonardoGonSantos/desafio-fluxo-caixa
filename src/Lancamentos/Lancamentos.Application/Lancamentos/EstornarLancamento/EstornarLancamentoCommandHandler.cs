using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;
using Lancamentos.Application.Abstractions;
using Lancamentos.Application.IntegrationEvents;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.Application.Lancamentos.EstornarLancamento;

public sealed class EstornarLancamentoCommandHandler(
    ILancamentoRepository repository,
    IOutbox outbox,
    TimeProvider timeProvider) : ICommandHandler<EstornarLancamentoCommand, LancamentoResponse>
{
    public async Task<Result<LancamentoResponse>> Handle(
        EstornarLancamentoCommand command, CancellationToken cancellationToken)
    {
        var original = await repository.GetByIdForUpdateAsync(command.LancamentoId, cancellationToken);
        if (original is null)
        {
            return LancamentoErrors.NaoEncontrado;
        }

        var result = original.Estornar(command.Motivo, timeProvider);
        if (result.IsFailure)
        {
            return result.Error;
        }

        var estorno = result.Value;
        await repository.UpdateStatusAsync(original, cancellationToken);
        await repository.AddAsync(estorno, cancellationToken);
        await outbox.EnqueueDomainEventsAsync(estorno, cancellationToken);

        return LancamentoResponse.From(estorno);
    }
}
