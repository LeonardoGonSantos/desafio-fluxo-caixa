using Consolidacao.Application.Abstractions;
using Dapper;

namespace Consolidacao.External.Persistence.Queries;

public sealed class SaldoQueries(DbSession session) : ISaldoQueries
{
    public async Task<SaldoDiaRow?> GetDiaAsync(DateOnly data, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            "SELECT data, total_creditos, total_debitos, atualizado_em FROM saldo_diario WHERE data = @data",
            new { data },
            cancellationToken: cancellationToken));

        return row?.ToDomain();
    }

    public async Task<decimal> GetSaldoAcumuladoAteAsync(DateOnly data, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT COALESCE(SUM(total_creditos - total_debitos), 0) FROM saldo_diario WHERE data <= @data",
            new { data },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SaldoDiaRow>> GetPeriodoAsync(
        DateOnly de, DateOnly ate, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT data, total_creditos, total_debitos, atualizado_em
            FROM saldo_diario
            WHERE data BETWEEN @de AND @ate
            ORDER BY data
            """,
            new { de, ate },
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToDomain()).ToList();
    }

    private sealed record Row(DateOnly Data, decimal TotalCreditos, decimal TotalDebitos, DateTime AtualizadoEm)
    {
        public SaldoDiaRow ToDomain() => new(
            Data, TotalCreditos, TotalDebitos, new DateTimeOffset(DateTime.SpecifyKind(AtualizadoEm, DateTimeKind.Utc)));
    }
}
