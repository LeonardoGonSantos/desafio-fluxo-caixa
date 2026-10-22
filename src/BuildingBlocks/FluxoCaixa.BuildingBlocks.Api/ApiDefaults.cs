using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FluxoCaixa.BuildingBlocks.Api;

/// <summary>Configuração comum das APIs: logs, JSON, ProblemDetails, correlação e health checks.</summary>
public static class ApiDefaults
{
    public static WebApplicationBuilder AddApiDefaults(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

        builder.Services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        return builder;
    }

    /// <summary>
    /// Health checks: readiness depende só do PostgreSQL; o broker indisponível apenas degrada
    /// <c>/health/dependencies</c> (o serviço continua aceitando requisições).
    /// </summary>
    public static IServiceCollection AddDefaultHealthChecks(this IServiceCollection services, string postgresConnectionString)
    {
        services.AddHealthChecks()
            .AddNpgSql(postgresConnectionString, name: "postgres", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["dependencies"]);
        return services;
    }

    public static WebApplication UseApiDefaults(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseExceptionHandler();
        return app;
    }

    public static WebApplication MapDefaultHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
        app.MapHealthChecks("/health/dependencies");
        return app;
    }
}
