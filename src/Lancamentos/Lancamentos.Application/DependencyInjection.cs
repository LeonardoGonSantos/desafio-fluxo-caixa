using FluxoCaixa.BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Lancamentos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services.AddCqrs(typeof(DependencyInjection).Assembly);
}
