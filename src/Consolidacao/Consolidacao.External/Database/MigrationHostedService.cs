using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Consolidacao.External.Database;

/// <summary>Roda as migrations no startup, com retry enquanto o Postgres sobe.</summary>
public sealed class MigrationHostedService(
    DatabaseMigrator migrator,
    ILogger<MigrationHostedService> logger) : IHostedService
{
    private const int MaxAttempts = 15;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                migrator.Migrate();
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning(ex, "Banco indisponível para migrations (tentativa {Attempt}/{Max}); nova tentativa em 3s",
                    attempt, MaxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
