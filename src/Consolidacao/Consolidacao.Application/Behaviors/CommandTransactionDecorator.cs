using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;
using Consolidacao.Application.Abstractions;

namespace Consolidacao.Application.Behaviors;

/// <summary>Executa o comando dentro de uma transação: commit só em caso de sucesso.</summary>
public sealed class CommandTransactionDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IUnitOfWork unitOfWork) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginAsync(cancellationToken);
        try
        {
            var result = await inner.Handle(command, cancellationToken);
            if (result.IsSuccess)
            {
                await unitOfWork.CommitAsync(cancellationToken);
            }
            else
            {
                await unitOfWork.RollbackAsync(cancellationToken);
            }

            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
