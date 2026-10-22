using Consolidacao.Api.Infrastructure;
using Consolidacao.Application.SaldosDiarios;
using Consolidacao.Application.SaldosDiarios.ObterRelatorioPeriodo;
using Consolidacao.Application.SaldosDiarios.ObterSaldoDiario;
using FluxoCaixa.BuildingBlocks.Cqrs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace Consolidacao.Api.Controllers;

[ApiController]
[Route("api/v1/saldos-diarios")]
[EnableRateLimiting(RateLimitingExtensions.SaldosPolicy)]
public sealed class SaldosDiariosController(
    IQueryHandler<ObterSaldoDiarioQuery, SaldoDiarioResponse> obterSaldo,
    IQueryHandler<ObterRelatorioPeriodoQuery, RelatorioPeriodoResponse> obterRelatorio) : ControllerBase
{
    /// <summary>Saldo consolidado de um dia (e o saldo acumulado até ele).</summary>
    /// <remarks>
    /// Consistência eventual: lançamentos recém-registrados aparecem em instantes, depois de processados
    /// de forma assíncrona. Dias sem movimento retornam 200 com totais zerados.
    /// </remarks>
    /// <param name="data">Data de competência (yyyy-MM-dd), não futura.</param>
    [HttpGet("{data}")]
    [ProducesResponseType<SaldoDiarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ObterPorData(DateOnly data, CancellationToken cancellationToken)
    {
        var result = await obterSaldo.Handle(new ObterSaldoDiarioQuery(data), cancellationToken);
        return this.ToActionResult(result, Ok);
    }

    /// <summary>Relatório diário de um período (máximo 93 dias), um item por dia.</summary>
    [HttpGet]
    [ProducesResponseType<RelatorioPeriodoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ObterPeriodo(
        [FromQuery, BindRequired] DateOnly de,
        [FromQuery, BindRequired] DateOnly ate,
        CancellationToken cancellationToken)
    {
        var result = await obterRelatorio.Handle(new ObterRelatorioPeriodoQuery(de, ate), cancellationToken);
        return this.ToActionResult(result, Ok);
    }
}
