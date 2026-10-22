using Dapper;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.External.Persistence.Repositories;

public sealed class LancamentoRepository(DbSession session) : ILancamentoRepository
{
    private const string InsertSql = """
        INSERT INTO lancamentos
            (id, tipo, valor, descricao, data_competencia, status, lancamento_original_id, criado_em)
        VALUES
            (@Id, @Tipo, @Valor, @Descricao, @DataCompetencia, @Status, @LancamentoOriginalId, @CriadoEm)
        """;

    private const string SelectForUpdateSql = """
        SELECT id, tipo, valor, descricao, data_competencia, status, lancamento_original_id, criado_em
        FROM lancamentos
        WHERE id = @id
        FOR UPDATE
        """;

    private const string UpdateStatusSql = "UPDATE lancamentos SET status = @Status WHERE id = @Id";

    public async Task AddAsync(Lancamento lancamento, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new
            {
                lancamento.Id,
                Tipo = (short)lancamento.Tipo,
                Valor = lancamento.Valor.Amount,
                Descricao = lancamento.Descricao.Valor,
                lancamento.DataCompetencia,
                Status = (short)lancamento.Status,
                lancamento.LancamentoOriginalId,
                lancamento.CriadoEm,
            },
            session.Transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<Lancamento?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<LancamentoRow>(new CommandDefinition(
            SelectForUpdateSql, new { id }, session.Transaction, cancellationToken: cancellationToken));

        return row is null
            ? null
            : Lancamento.Rehydrate(
                row.Id,
                (TipoLancamento)row.Tipo,
                row.Valor,
                row.Descricao,
                row.DataCompetencia,
                (StatusLancamento)row.Status,
                row.LancamentoOriginalId,
                new DateTimeOffset(DateTime.SpecifyKind(row.CriadoEm, DateTimeKind.Utc)));
    }

    public async Task UpdateStatusAsync(Lancamento lancamento, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            UpdateStatusSql,
            new { lancamento.Id, Status = (short)lancamento.Status },
            session.Transaction,
            cancellationToken: cancellationToken));
    }

    private sealed record LancamentoRow(
        Guid Id,
        short Tipo,
        decimal Valor,
        string Descricao,
        DateOnly DataCompetencia,
        short Status,
        Guid? LancamentoOriginalId,
        DateTime CriadoEm);
}
