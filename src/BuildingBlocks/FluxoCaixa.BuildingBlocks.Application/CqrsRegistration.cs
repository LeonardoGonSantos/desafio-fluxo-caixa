using System.Reflection;
using FluentValidation;
using FluxoCaixa.BuildingBlocks.Application.Behaviors;
using FluxoCaixa.BuildingBlocks.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace FluxoCaixa.BuildingBlocks.Application;

public static class CqrsRegistration
{
    /// <summary>
    /// Registra handlers (commands e queries) e validators do assembly informado e aplica os decorators.
    /// Ordem: o último decorator registrado é o mais externo → Logging → Validação → Transação → Handler.
    /// </summary>
    public static IServiceCollection AddCqrs(this IServiceCollection services, Assembly assembly)
    {
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.Scan(scan => scan.FromAssemblies(assembly)
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)))
                .AsImplementedInterfaces().WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)))
                .AsImplementedInterfaces().WithScopedLifetime());

        services.Decorate(typeof(ICommandHandler<,>), typeof(CommandTransactionDecorator<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(CommandValidationDecorator<,>));
        services.Decorate(typeof(ICommandHandler<,>), typeof(CommandLoggingDecorator<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(QueryValidationDecorator<,>));
        services.Decorate(typeof(IQueryHandler<,>), typeof(QueryLoggingDecorator<,>));

        return services;
    }
}
