using Dapper;
using Lancamentos.External.Outbox;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RabbitMQ.Client;

namespace Lancamentos.IntegrationTests;

/// <summary>
/// API de Lançamentos em memória apontando para containers reais.
/// Cada instância usa um banco novo; o loop em background do outbox fica desligado
/// (Outbox:Enabled=false) para que os testes dirijam o processamento de forma determinística.
/// </summary>
public sealed class LancamentosApiFactory : WebApplicationFactory<Program>
{
    private readonly InfraFixture _infra;
    private readonly bool _rabbitAvailable;
    private readonly bool _backgroundOutbox;
    private readonly string _database = $"lancamentos_{Guid.NewGuid():N}";

    /// <summary>Exchange exclusivo por instância: isola filas/bindings entre testes.</summary>
    public string Exchange { get; } = $"fluxo.test.{Guid.NewGuid():N}";

    public LancamentosApiFactory(InfraFixture infra, bool rabbitAvailable = true, bool backgroundOutbox = false)
    {
        _infra = infra;
        _rabbitAvailable = rabbitAvailable;
        _backgroundOutbox = backgroundOutbox;
    }

    public string ConnectionString => new NpgsqlConnectionStringBuilder(_infra.Postgres.GetConnectionString())
    {
        Database = _database,
    }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Lancamentos", ConnectionString);
        builder.UseSetting("Outbox:Enabled", _backgroundOutbox ? "true" : "false");
        builder.UseSetting("Outbox:PollingInterval", "00:00:00.200");
        builder.UseSetting("Swagger:Enabled", "true");

        // Broker "fora do ar": porta onde ninguém escuta.
        builder.UseSetting("RabbitMq:Host", _rabbitAvailable ? _infra.Rabbit.Hostname : "127.0.0.1");
        builder.UseSetting("RabbitMq:Port", (_rabbitAvailable ? _infra.Rabbit.GetMappedPublicPort(5672) : 1).ToString());
        builder.UseSetting("RabbitMq:Exchange", Exchange);
        builder.UseSetting("RabbitMq:Username", "fluxo");
        builder.UseSetting("RabbitMq:Password", "fluxo");
    }

    public async Task<IChannel> CreateRabbitChannelAsync()
    {
        var factory = new ConnectionFactory
        {
            HostName = _infra.Rabbit.Hostname,
            Port = _infra.Rabbit.GetMappedPublicPort(5672),
            UserName = "fluxo",
            Password = "fluxo",
        };
        var connection = await factory.CreateConnectionAsync();
        return await connection.CreateChannelAsync();
    }

    public OutboxProcessor Outbox => Services.GetRequiredService<OutboxProcessor>();

    public async Task<T> QueryScalarAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<T>(sql, parameters) ?? default!;
    }
}
