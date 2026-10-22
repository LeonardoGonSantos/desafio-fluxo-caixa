using FluxoCaixa.BuildingBlocks.Api;
using Lancamentos.Api.Filters;
using Lancamentos.Api.Infrastructure;
using Lancamentos.Application;
using Lancamentos.External;

var builder = WebApplication.CreateBuilder(args).AddApiDefaults();

builder.Services.AddApplication();
builder.Services.AddExternal(builder.Configuration);
builder.Services.AddScoped<IdempotencyFilter>();
builder.Services.AddSwagger();
builder.Services.AddDefaultHealthChecks(
    builder.Configuration.GetConnectionString(Lancamentos.External.DependencyInjection.ConnectionStringName)!);

var app = builder.Build();

app.UseApiDefaults();
app.UseSwaggerIfEnabled();
app.MapControllers();
app.MapDefaultHealthChecks();

app.Run();

public partial class Program;
