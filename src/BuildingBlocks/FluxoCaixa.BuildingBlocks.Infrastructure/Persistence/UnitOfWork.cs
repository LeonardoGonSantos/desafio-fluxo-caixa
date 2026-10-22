using FluxoCaixa.BuildingBlocks.Application.Abstractions;

namespace FluxoCaixa.BuildingBlocks.Infrastructure.Persistence;

public sealed class UnitOfWork(DbSession session) : IUnitOfWork
{
    public Task BeginAsync(CancellationToken cancellationToken) => session.BeginAsync(cancellationToken);

    public Task CommitAsync(CancellationToken cancellationToken) => session.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken) => session.RollbackAsync(cancellationToken);
}
