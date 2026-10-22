using FluxoCaixa.BuildingBlocks.Results;

namespace FluxoCaixa.BuildingBlocks.Cqrs;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
