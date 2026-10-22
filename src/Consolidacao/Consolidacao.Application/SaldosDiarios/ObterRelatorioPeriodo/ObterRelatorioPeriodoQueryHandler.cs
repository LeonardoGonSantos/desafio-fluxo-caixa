using Consolidacao.Application.Abstractions;
using FluxoCaixa.BuildingBlocks.Cqrs;
using FluxoCaixa.BuildingBlocks.Results;

namespace Consolidacao.Application.SaldosDiarios.ObterRelatorioPeriodo;

public sealed class ObterRelatorioPeriodoQueryHandler(ISaldoQueries queries)
    : IQueryHandler<ObterRelatorioPeriodoQuery, RelatorioPeriodoResponse>
{
    public async Task<Result<RelatorioPeriodoResponse>> Handle(
        ObterRelatorioPeriodoQuery query, CancellationToken cancellationToken)
    {
        var acumulado = await queries.GetSaldoAcumuladoAteAsync(query.De.AddDays(-1), cancellationToken);
        var porDia = (await queries.GetPeriodoAsync(query.De, query.Ate, cancellationToken))
            .ToDictionary(r => r.Data);

        var dias = new List<SaldoDiarioResponse>();
        decimal totalCreditos = 0, totalDebitos = 0;

        for (var data = query.De; data <= query.Ate; data = data.AddDays(1))
        {
            porDia.TryGetValue(data, out var linha);
            var creditos = linha?.TotalCreditos ?? 0m;
            var debitos = linha?.TotalDebitos ?? 0m;

            acumulado += creditos - debitos;
            totalCreditos += creditos;
            totalDebitos += debitos;
            dias.Add(new SaldoDiarioResponse(data, creditos, debitos, creditos - debitos, acumulado, linha?.AtualizadoEm));
        }

        return new RelatorioPeriodoResponse(
            query.De, query.Ate, totalCreditos, totalDebitos, totalCreditos - totalDebitos, dias);
    }
}
