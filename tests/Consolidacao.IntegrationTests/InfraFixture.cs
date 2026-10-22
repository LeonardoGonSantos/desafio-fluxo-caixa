using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Consolidacao.IntegrationTests;

/// <summary>Sobe PostgreSQL e RabbitMQ reais uma única vez para todos os testes de integração.</summary>
public sealed class InfraFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("postgres:17-alpine")
        .WithUsername("fluxo").WithPassword("fluxo").Build();

    public RabbitMqContainer Rabbit { get; } = new RabbitMqBuilder("rabbitmq:4-management-alpine")
        .WithUsername("fluxo").WithPassword("fluxo").Build();

    public async Task InitializeAsync() =>
        await Task.WhenAll(Postgres.StartAsync(), Rabbit.StartAsync());

    public async Task DisposeAsync()
    {
        await Postgres.DisposeAsync();
        await Rabbit.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class InfraCollection : ICollectionFixture<InfraFixture>
{
    public const string Name = "infra";
}
