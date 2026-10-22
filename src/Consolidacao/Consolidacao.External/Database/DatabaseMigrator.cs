using DbUp;
using Microsoft.Extensions.Logging;

namespace Consolidacao.External.Database;

/// <summary>Aplica os scripts SQL embutidos (Database/Scripts) em ordem; cada um roda uma única vez.</summary>
public sealed class DatabaseMigrator(string connectionString, ILogger<DatabaseMigrator> logger)
{
    public void Migrate()
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DatabaseMigrator).Assembly,
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
