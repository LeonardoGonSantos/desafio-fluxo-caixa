using FluxoCaixa.BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Consolidacao.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services.AddCqrs(typeof(DependencyInjection).Assembly);
}
