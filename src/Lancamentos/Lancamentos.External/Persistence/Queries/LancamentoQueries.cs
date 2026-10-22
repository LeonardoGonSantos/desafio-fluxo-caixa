using Dapper;
using FluxoCaixa.BuildingBlocks.Infrastructure.Persistence;
using Lancamentos.Application.Abstractions;
using Lancamentos.Application.Common;
using Lancamentos.Application.Lancamentos;
using Lancamentos.Domain.Lancamentos;

namespace Lancamentos.External.Persistence.Queries;

public sealed class LancamentoQueries(DbSession session) : ILancamentoQueries
{
    private const string Columns =
        "id, tipo, valor, descricao, data_competencia, status, lancamento_original_id, criado_em";

    public async Task<LancamentoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM lancamentos WHERE id = @id",
            new { id },
            cancellationToken: cancellationToken));

        return row?.ToResponse();
    }

    public async Task<IReadOnlyList<LancamentoResponse>> ListAsync(
        DateOnly de, DateOnly ate, TipoLancamento? tipo, int limit, LancamentoCursor? after, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);

        // Comparação de linha (row value) casa com o índice (data_competencia, criado_em, id) DESC: sem OFFSET.
        var cursorFilter = after is null
            ? string.Empty
            : "AND (data_competencia, criado_em, id) < (@cursorData, @cursorCriadoEm, @cursorId)";

        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            $"""
             SELECT {Columns} FROM lancamentos
             WHERE data_competencia BETWEEN @de AND @ate
               AND (@tipo::smallint IS NULL OR tipo = @tipo::smallint)
               {cursorFilter}
             ORDER BY data_competencia DESC, criado_em DESC, id DESC
             LIMIT @limit
             """,
            new
            {
                de,
                ate,
                tipo = tipo.HasValue ? (short?)tipo.Value : null,
                limit,
                cursorData = after?.DataCompetencia,
                cursorCriadoEm = after?.CriadoEm,
                cursorId = after?.Id,
            },
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToResponse()).ToList();
    }

    private sealed record Row(
        Guid Id,
        short Tipo,
        decimal Valor,
        string Descricao,
        DateOnly DataCompetencia,
        short Status,
        Guid? LancamentoOriginalId,
        DateTime CriadoEm)
    {
        public LancamentoResponse ToResponse() => new(
            Id,
            ((TipoLancamento)Tipo).ToString(),
            Valor,
            Descricao,
            DataCompetencia,
            ((StatusLancamento)Status).ToString(),
            LancamentoOriginalId,
            new DateTimeOffset(DateTime.SpecifyKind(CriadoEm, DateTimeKind.Utc)));
    }
}
