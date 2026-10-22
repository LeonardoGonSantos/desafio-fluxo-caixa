using FluentValidation;
using FluxoCaixa.BuildingBlocks.Cqrs;
using Consolidacao.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace Consolidacao.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)))
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)))
                .AsImplementedInterfaces().WithScopedLifetime());

        // Ordem: o último decorator registrado é o mais externo.
        // Logging → Validação → Transação → Handler
        services.Decorate(typeof(ICommandHandler<,>), typeof(CommandTransactionDecorator<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(CommandValidationDecorator<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(CommandLoggingDecorator<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(QueryValidationDecorator<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(QueryLoggingDecorator<,>));

        return services;
    }
}
