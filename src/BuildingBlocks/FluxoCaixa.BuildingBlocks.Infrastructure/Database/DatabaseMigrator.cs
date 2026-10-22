using System.Reflection;
using DbUp;
using Microsoft.Extensions.Logging;

namespace FluxoCaixa.BuildingBlocks.Infrastructure.Database;

/// <summary>
/// Aplica, em ordem, os scripts SQL embutidos em <c>*.Database.Scripts.*</c> do assembly informado;
/// cada script roda uma única vez (tabela schemaversions do DbUp).
/// </summary>
public sealed class DatabaseMigrator(string connectionString, Assembly scriptsAssembly, ILogger<DatabaseMigrator> logger)
{
    public void Migrate()
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                scriptsAssembly,
                name => name.Contains(".Database.Scripts.", StringComparison.Ordinal))
            .LogToNowhere()
            .Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException("Falha ao aplicar scripts de banco.", result.Error);
        }

        logger.LogInformation(
            "Migrations de banco aplicadas ({Count} script(s) novo(s))", result.Scripts.Count());
    }
}
