using System.Diagnostics;
using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;
using Microsoft.Extensions.Logging;

namespace Consolidacao.Application.Behaviors;

public sealed class CommandLoggingDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<CommandLoggingDecorator<TCommand, TResponse>> logger) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
        await LoggingRunner.RunAsync(logger, typeof(TCommand).Name, () => inner.Handle(command, cancellationToken));
}

public sealed class QueryLoggingDecorator<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    ILogger<QueryLoggingDecorator<TQuery, TResponse>> logger) : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken) =>
        await LoggingRunner.RunAsync(logger, typeof(TQuery).Name, () => inner.Handle(query, cancellationToken));
}

internal static class LoggingRunner
{
    public static async Task<Result<T>> RunAsync<T>(ILogger logger, string useCase, Func<Task<Result<T>>> next)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await next();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (result.IsSuccess)
            {
                logger.LogInformation("{UseCase} concluído em {ElapsedMs:F1}ms", useCase, elapsed);
            }
            else
            {
                logger.LogWarning(
                    "{UseCase} falhou em {ElapsedMs:F1}ms: {ErrorCode} ({ErrorType})",
                    useCase, elapsed, result.Error.Code, result.Error.Type);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{UseCase} lançou exceção após {ElapsedMs:F1}ms",
                useCase, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}
