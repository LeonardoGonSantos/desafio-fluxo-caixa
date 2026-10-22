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

    public async Task<PagedResult<LancamentoResponse>> ListAsync(
        DateOnly de, DateOnly ate, TipoLancamento? tipo, int page, int pageSize, CancellationToken cancellationToken)
    {
        var connection = await session.GetConnectionAsync(cancellationToken);
        var parameters = new
        {
            de,
            ate,
            tipo = tipo.HasValue ? (short?)tipo.Value : null,
            limit = pageSize,
            offset = (page - 1) * pageSize,
        };

        const string filter = """
            WHERE data_competencia BETWEEN @de AND @ate
              AND (@tipo::smallint IS NULL OR tipo = @tipo::smallint)
            """;

        var total = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"SELECT count(*) FROM lancamentos {filter}", parameters, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            $"""
             SELECT {Columns} FROM lancamentos {filter}
             ORDER BY data_competencia DESC, criado_em DESC, id
             LIMIT @limit OFFSET @offset
             """,
            parameters,
            cancellationToken: cancellationToken));

        return new PagedResult<LancamentoResponse>(
            rows.Select(r => r.ToResponse()).ToList(), page, pageSize, total);
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
