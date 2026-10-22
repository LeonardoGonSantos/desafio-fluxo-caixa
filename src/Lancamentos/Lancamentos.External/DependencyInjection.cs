using Lancamentos.Application.Abstractions;
using Lancamentos.External.Idempotency;
using Lancamentos.External.Messaging;
using Lancamentos.External.Outbox;
using Lancamentos.External.Resilience;
using Lancamentos.External.Persistence.Queries;
using Lancamentos.External.Persistence.Repositories;
using Lancamentos.Domain.Lancamentos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using FluxoCaixa.BuildingBlocks.Application.Abstractions;
using FluxoCaixa.BuildingBlocks.Infrastructure.Database;
using FluxoCaixa.BuildingBlocks.Infrastructure.Messaging;
using FluxoCaixa.BuildingBlocks.Infrastructure.Persistence;

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

        services.AddSingleton(sp => new RabbitMqConnectionProvider(
            sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value, "lancamentos-api"));
        services.AddPublishResilience(configuration);
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

        services.AddSingleton(sp => new DatabaseMigrator(
            connectionString, typeof(DependencyInjection).Assembly, sp.GetRequiredService<ILogger<DatabaseMigrator>>()));

        // A ordem importa: as migrations rodam antes do processador de outbox.
        services.AddHostedService<MigrationHostedService>();
        services.AddSingleton<IOutboxStore, PostgresOutboxStore>();
        services.AddSingleton<OutboxProcessor>();

        // Permite desligar o loop em segundo plano (ex.: réplicas só de API, ou testes que conduzem o processamento).
        if (configuration.GetValue("Outbox:Enabled", true))
        {
            services.AddHostedService(sp => sp.GetRequiredService<OutboxProcessor>());
        }

        return services;
    }
}
