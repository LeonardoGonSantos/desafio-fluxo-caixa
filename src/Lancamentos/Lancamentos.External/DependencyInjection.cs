using Lancamentos.Application.Abstractions;
using Lancamentos.External.Database;
using Lancamentos.External.Idempotency;
using Lancamentos.External.Messaging;
using Lancamentos.External.Outbox;
using Lancamentos.External.Persistence;
using Lancamentos.External.Persistence.Queries;
using Lancamentos.External.Persistence.Repositories;
using Lancamentos.Domain.Lancamentos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Lancamentos.External;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Lancamentos";

    public static IServiceCollection AddExternal(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} não configurada.");

        DapperConfiguration.Configure();
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());

        services.AddScoped<DbSession>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ILancamentoRepository, LancamentoRepository>();
        services.AddScoped<IOutbox, OutboxRepository>();
        services.AddScoped<ILancamentoQueries, LancamentoQueries>();
        services.AddSingleton<IIdempotencyStore, PostgresIdempotencyStore>();

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

        services.AddSingleton(sp => new DatabaseMigrator(connectionString, sp.GetRequiredService<ILogger<DatabaseMigrator>>()));

        // A ordem importa: as migrations rodam antes do processador de outbox.
        services.AddHostedService<MigrationHostedService>();
        services.AddSingleton<OutboxProcessor>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxProcessor>());

        return services;
    }
}
