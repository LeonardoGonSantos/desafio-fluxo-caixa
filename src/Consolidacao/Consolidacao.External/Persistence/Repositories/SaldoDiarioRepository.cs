using Consolidacao.Domain.SaldosDiarios;
using Dapper;

namespace Consolidacao.External.Persistence.Repositories;

public sealed class SaldoDiarioRepository(DbSession session) : ISaldoDiarioRepository
{
    private const string EnsureRowSql = """
        INSERT INTO saldo_diario (data, total_creditos, total_debitos, atualizado_em)
        VALUES (@data, 0, 0, @agora)
        ON CONFLICT (data) DO NOTHING
        """;

    private const string SelectForUpdateSql = """
        SELECT data, total_creditos, total_debitos, atualizado_em
        FROM saldo_diario
        WHERE data = @data
        FOR UPDATE
        """;

    private const string UpdateSql = """
        UPDATE saldo_diario
        SET total_creditos = @TotalCreditos, total_debitos = @TotalDebitos, atualizado_em = @AtualizadoEm
        WHERE data = @Data
        """;

    public async Task<SaldoDiario> GetForUpdateOrCreateAsync(
        DateOnly data, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            EnsureRowSql, new { data, agora }, session.Transaction, cancellationToken: cancellationToken));

        var row = await connection.QuerySingleAsync<Row>(new CommandDefinition(
            SelectForUpdateSql, new { data }, session.Transaction, cancellationToken: cancellationToken));

        return SaldoDiario.Rehydrate(
            row.Data,
            row.TotalCreditos,
            row.TotalDebitos,
            new DateTimeOffset(DateTime.SpecifyKind(row.AtualizadoEm, DateTimeKind.Utc)));
    }

    public async Task SaveAsync(SaldoDiario saldo, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            UpdateSql,
            new
            {
                saldo.Data,
                TotalCreditos = saldo.TotalCreditos.Amount,
                TotalDebitos = saldo.TotalDebitos.Amount,
                saldo.AtualizadoEm,
            },
            session.Transaction,
            cancellationToken: cancellationToken));
    }

    private sealed record Row(DateOnly Data, decimal TotalCreditos, decimal TotalDebitos, DateTime AtualizadoEm);
}
