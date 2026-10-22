using System.Text;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RabbitMQ.Client;

namespace Consolidacao.IntegrationTests;

/// <summary>
/// API de Consolidação em memória (com o consumer rodando) apontando para containers reais.
/// Banco, exchange e filas exclusivos por instância isolam os testes entre si.
/// </summary>
public sealed class ConsolidacaoApiFactory : WebApplicationFactory<Program>
{
    public const string RoutingKey = "lancamento.registrado.v1";

    private readonly InfraFixture _infra;
    private readonly int _tokenLimit;
    private readonly Action<IServiceCollection>? _overrideServices;
    private readonly string _id = Guid.NewGuid().ToString("N");

    public ConsolidacaoApiFactory(
        InfraFixture infra, int tokenLimit = 10_000, Action<IServiceCollection>? overrideServices = null)
    {
        _infra = infra;
        _tokenLimit = tokenLimit;
        _overrideServices = overrideServices;
    }

    public string Exchange => $"fluxo.test.{_id}";

    public string Queue => $"consolidacao.test.{_id}";

    public string DeadLetterQueue => $"consolidacao.test.{_id}.dlq";

    public string ConnectionString => new NpgsqlConnectionStringBuilder(_infra.Postgres.GetConnectionString())
    {
        Database = $"consolidacao_{_id}",
    }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Consolidacao", ConnectionString);
        builder.UseSetting("Swagger:Enabled", "true");
        builder.UseSetting("RateLimiting:TokenLimit", _tokenLimit.ToString());
        builder.UseSetting("RateLimiting:TokensPerSecond", _tokenLimit.ToString());

        builder.UseSetting("RabbitMq:Host", _infra.Rabbit.Hostname);
        builder.UseSetting("RabbitMq:Port", _infra.Rabbit.GetMappedPublicPort(5672).ToString());
        builder.UseSetting("RabbitMq:Username", "fluxo");
        builder.UseSetting("RabbitMq:Password", "fluxo");
        builder.UseSetting("RabbitMq:Exchange", Exchange);
        builder.UseSetting("RabbitMq:Queue", Queue);
        builder.UseSetting("RabbitMq:DeadLetterExchange", $"fluxo.test.{_id}.dlx");
        builder.UseSetting("RabbitMq:DeadLetterQueue", DeadLetterQueue);
        builder.UseSetting("RabbitMq:RetryDelay", "00:00:00.100");
        builder.UseSetting("RabbitMq:MaxProcessingAttempts", "3");
        builder.UseSetting("RabbitMq:RecoveryGracePeriod", "00:00:20");

        if (_overrideServices is not null)
        {
            builder.ConfigureTestServices(_overrideServices);
        }
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

    /// <summary>Simula uma queda de rede: o broker derruba todas as conexões abertas.</summary>
    public async Task CloseAllBrokerConnectionsAsync()
    {
        var result = await _infra.Rabbit.ExecAsync(["rabbitmqctl", "close_all_connections", "queda simulada no teste"]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"rabbitmqctl falhou: {result.Stderr}");
        }
    }

    /// <summary>
    /// Aguarda o consumer estar REGISTRADO na fila (ele sobe em background no startup). Existência da fila não basta:
    /// ela é declarada antes do binding com o exchange, e publicar nesse intervalo perderia a mensagem (sem rota).
    /// Um passive-declare de fila inexistente fecha o canal no broker, então cada sonda usa um canal novo.
    /// </summary>
    public async Task WaitForConsumerAsync()
    {
        await Eventually.ForAsync(async () =>
        {
            try
            {
                await using var probe = await CreateRabbitChannelAsync();
                var fila = await probe.QueueDeclarePassiveAsync(Queue);
                return fila.ConsumerCount >= 1;
            }
            catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
            {
                return false;
            }
        });
    }

    public async Task PublishAsync(
        IChannel channel, string body, string? type = RoutingKey, Guid? messageId = null)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Type = type,
            MessageId = (messageId ?? Guid.NewGuid()).ToString(),
        };
        await channel.BasicPublishAsync(Exchange, RoutingKey, mandatory: false, properties, Encoding.UTF8.GetBytes(body));
    }

    public async Task<T> QueryScalarAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<T>(sql, parameters) ?? default!;
    }

    public static string Evento(Guid eventId, string tipo, decimal valor, string data) =>
        $$"""
        {"eventId":"{{eventId}}","occurredAt":"2025-06-10T15:00:00+00:00","lancamentoId":"{{Guid.NewGuid()}}",
         "tipo":"{{tipo}}","valor":{{valor.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"dataCompetencia":"{{data}}"}
        """;
}

public static class Eventually
{
    public static async Task ForAsync(Func<Task<bool>> condition, int timeoutSeconds = 60)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Condição não satisfeita em {timeoutSeconds}s.");
    }
}
