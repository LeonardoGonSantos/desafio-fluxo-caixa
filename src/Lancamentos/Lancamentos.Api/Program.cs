using System.Text.Json.Serialization;
using Lancamentos.Api.Filters;
using Lancamentos.Api.Infrastructure;
using Lancamentos.Application;
using Lancamentos.External;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddApplication();
builder.Services.AddExternal(builder.Configuration);
builder.Services.AddScoped<IdempotencyFilter>();
builder.Services.AddSwagger();

builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString(Lancamentos.External.DependencyInjection.ConnectionStringName)!,
        name: "postgres",
        tags: ["ready"])
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["dependencies"]);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseSwaggerIfEnabled();

app.MapControllers();

// Liveness: o processo responde. Readiness: só o banco é imprescindível — broker fora não tira o serviço do ar.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/health/dependencies");

app.Run();

public partial class Program;
