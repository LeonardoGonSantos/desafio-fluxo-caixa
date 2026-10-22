using Consolidacao.Api.Infrastructure;
using Consolidacao.Application;
using Consolidacao.External;
using FluxoCaixa.BuildingBlocks.Api;

var builder = WebApplication.CreateBuilder(args).AddApiDefaults();

builder.Services.AddApplication();
builder.Services.AddExternal(builder.Configuration);
builder.Services.AddSaldosRateLimiting(builder.Configuration);
builder.Services.AddSwagger();
builder.Services.AddDefaultHealthChecks(
    builder.Configuration.GetConnectionString(Consolidacao.External.DependencyInjection.ConnectionStringName)!);

var app = builder.Build();

app.UseApiDefaults();
app.UseRateLimiter();
app.UseSwaggerIfEnabled();
app.MapControllers();
app.MapDefaultHealthChecks();

app.Run();

public partial class Program;
