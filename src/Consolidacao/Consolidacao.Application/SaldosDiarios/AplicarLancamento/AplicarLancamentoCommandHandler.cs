using Consolidacao.Application.Abstractions;
using Consolidacao.Domain.SaldosDiarios;
using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;

namespace Consolidacao.Application.SaldosDiarios.AplicarLancamento;

public sealed class AplicarLancamentoCommandHandler(
    IProcessedEventStore processedEvents,
    ISaldoDiarioRepository repository,
    TimeProvider timeProvider) : ICommandHandler<AplicarLancamentoCommand, AplicarLancamentoResponse>
{
    public async Task<Result<AplicarLancamentoResponse>> Handle(
        AplicarLancamentoCommand command, CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow();

        // Registro e atualização do saldo na MESMA transação: ou os dois acontecem, ou nenhum.
        if (!await processedEvents.TryRegisterAsync(command.EventId, agora, cancellationToken))
        {
            return new AplicarLancamentoResponse(false);
        }

        var saldo = await repository.GetForUpdateOrCreateAsync(command.DataCompetencia, agora, cancellationToken);
        var result = saldo.Aplicar(command.Tipo, command.Valor, agora);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await repository.SaveAsync(saldo, cancellationToken);
        return new AplicarLancamentoResponse(true);
    }
}
