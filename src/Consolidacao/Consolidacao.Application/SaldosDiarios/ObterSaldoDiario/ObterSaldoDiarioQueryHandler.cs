using Consolidacao.Application.Abstractions;
using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;

namespace Consolidacao.Application.SaldosDiarios.ObterSaldoDiario;

public sealed class ObterSaldoDiarioQueryHandler(ISaldoQueries queries, ISaldoCache cache)
    : IQueryHandler<ObterSaldoDiarioQuery, SaldoDiarioResponse>
{
    public async Task<Result<SaldoDiarioResponse>> Handle(ObterSaldoDiarioQuery query, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"saldo:{query.Data:yyyy-MM-dd}",
            ct => CarregarAsync(query.Data, ct),
            cancellationToken);

    private async Task<SaldoDiarioResponse> CarregarAsync(DateOnly data, CancellationToken cancellationToken)
    {
        var dia = await queries.GetDiaAsync(data, cancellationToken);
        var acumulado = await queries.GetSaldoAcumuladoAteAsync(data, cancellationToken);

        // Dia sem movimento não é erro: o relatório existe para qualquer data passada.
        return dia is null
            ? new SaldoDiarioResponse(data, 0m, 0m, 0m, acumulado, null)
            : new SaldoDiarioResponse(
                data, dia.TotalCreditos, dia.TotalDebitos, dia.TotalCreditos - dia.TotalDebitos, acumulado, dia.AtualizadoEm);
    }
}
