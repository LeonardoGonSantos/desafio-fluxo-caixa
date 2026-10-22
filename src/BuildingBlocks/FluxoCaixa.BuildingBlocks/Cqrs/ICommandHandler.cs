using FluxoCaixa.BuildingBlocks.Results;

namespace FluxoCaixa.BuildingBlocks.Cqrs;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
