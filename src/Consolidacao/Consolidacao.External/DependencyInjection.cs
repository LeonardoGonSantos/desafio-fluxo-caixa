using Consolidacao.Application.Abstractions;
using Consolidacao.Domain.SaldosDiarios;
using Consolidacao.External.Caching;
using Consolidacao.External.Messaging;
using Consolidacao.External.Persistence.Queries;
using Consolidacao.External.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using FluxoCaixa.BuildingBlocks.Application.Abstractions;
using FluxoCaixa.BuildingBlocks.Infrastructure.Database;
using FluxoCaixa.BuildingBlocks.Infrastructure.Messaging;
using FluxoCaixa.BuildingBlocks.Infrastructure.Persistence;

namespace Consolidacao.External;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Consolidacao";

    public static IServiceCollection AddExternal(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} não configurada.");

        DapperConfiguration.Configure();
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());

        services.AddScoped<DbSession>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISaldoDiarioRepository, SaldoDiarioRepository>();
        services.AddScoped<IProcessedEventStore, ProcessedEventStore>();
        services.AddScoped<ISaldoQueries, SaldoQueries>();

        services.AddHybridCache();
        services.AddSingleton<ISaldoCache, SaldoCache>();

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp => new RabbitMqConnectionProvider(
            sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value, "consolidacao-api"));
        services.AddSingleton(sp => new DatabaseMigrator(
            connectionString, typeof(DependencyInjection).Assembly, sp.GetRequiredService<ILogger<DatabaseMigrator>>()));

        // A ordem importa: as migrations rodam antes do consumidor.
        services.AddHostedService<MigrationHostedService>();
        services.AddSingleton<ConsumerStatus>();
        services.AddSingleton<LancamentoRegistradoMessageHandler>();
        services.AddHostedService<RabbitMqConsumerHostedService>();

        return services;
    }
}
