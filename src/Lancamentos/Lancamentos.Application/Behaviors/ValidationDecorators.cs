using FluentValidation;
using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;

namespace Lancamentos.Application.Behaviors;

internal static class ValidationRunner
{
    public static async Task<Error?> ValidateAsync<T>(
        IEnumerable<IValidator<T>> validators, T instance, CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(instance, cancellationToken);
            failures.AddRange(result.Errors.Select(e => e.ErrorMessage));
        }

        return failures.Count == 0
            ? null
            : Error.Validation("validation.failed", string.Join(" ", failures.Distinct()));
    }
}

public sealed class CommandValidationDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var error = await ValidationRunner.ValidateAsync(validators, command, cancellationToken);
        return error is not null ? error : await inner.Handle(command, cancellationToken);
    }
}

public sealed class QueryValidationDecorator<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        var error = await ValidationRunner.ValidateAsync(validators, query, cancellationToken);
        return error is not null ? error : await inner.Handle(query, cancellationToken);
    }
}
