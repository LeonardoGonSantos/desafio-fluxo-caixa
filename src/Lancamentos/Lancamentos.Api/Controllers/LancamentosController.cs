using FluxoCaixa.BuildingBlocks.Cqrs;
using Lancamentos.Api.Contracts;
using Lancamentos.Api.Filters;
using FluxoCaixa.BuildingBlocks.Api;
using Lancamentos.Api.Infrastructure;
using Lancamentos.Application.Common;
using Lancamentos.Application.Lancamentos;
using Lancamentos.Application.Lancamentos.EstornarLancamento;
using Lancamentos.Application.Lancamentos.ListarLancamentos;
using Lancamentos.Application.Lancamentos.ObterLancamentoPorId;
using Lancamentos.Application.Lancamentos.RegistrarLancamento;
using Lancamentos.Domain.Lancamentos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Lancamentos.Api.Controllers;

[ApiController]
[Route("api/v1/lancamentos")]
public sealed class LancamentosController(
    ICommandHandler<RegistrarLancamentoCommand, LancamentoResponse> registrar,
    ICommandHandler<EstornarLancamentoCommand, LancamentoResponse> estornar,
    IQueryHandler<ObterLancamentoPorIdQuery, LancamentoResponse> obterPorId,
    IQueryHandler<ListarLancamentosQuery, CursorPage<LancamentoResponse>> listar) : ControllerBase
{
    /// <summary>Registra um crédito ou débito no fluxo de caixa.</summary>
    /// <remarks>
    /// Exige o header <c>Idempotency-Key</c>: repetir a chamada com a mesma chave e o mesmo corpo
    /// devolve a resposta original sem criar um novo lançamento.
    /// </remarks>
    [HttpPost]
    [ServiceFilter<IdempotencyFilter>]
    [ProducesResponseType<LancamentoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Registrar(RegistrarLancamentoRequest request, CancellationToken cancellationToken)
    {
        var result = await registrar.Handle(
            new RegistrarLancamentoCommand(request.Tipo, request.Valor, request.Descricao, request.DataCompetencia),
            cancellationToken);

        return this.ToActionResult(result, r => Created(LocationOf(r.Id), r));
    }

    /// <summary>Estorna um lançamento criando um lançamento inverso (lançamentos nunca são editados ou excluídos).</summary>
    [HttpPost("{id:guid}/estornos")]
    [ProducesResponseType<LancamentoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Estornar(Guid id, EstornarLancamentoRequest request, CancellationToken cancellationToken)
    {
        var result = await estornar.Handle(new EstornarLancamentoCommand(id, request.Motivo), cancellationToken);
        return this.ToActionResult(result, r => Created(LocationOf(r.Id), r));
    }

    /// <summary>Consulta um lançamento pelo identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<LancamentoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        var result = await obterPorId.Handle(new ObterLancamentoPorIdQuery(id), cancellationToken);
        return this.ToActionResult(result, Ok);
    }

    /// <summary>Lista lançamentos por período de competência (máximo 93 dias), paginado por cursor.</summary>
    /// <remarks>
    /// Mais recentes primeiro. Para a próxima página, repita a chamada com o <c>nextCursor</c> recebido no parâmetro
    /// <c>cursor</c> (opaco; ausente na última página).
    /// </remarks>
    /// <param name="cursor">Cursor devolvido em <c>nextCursor</c> pela página anterior.</param>
    [HttpGet]
    [ProducesResponseType<CursorPage<LancamentoResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery, BindRequired] DateOnly de,
        [FromQuery, BindRequired] DateOnly ate,
        [FromQuery] TipoLancamento? tipo,
        CancellationToken cancellationToken,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? cursor = null)
    {
        var result = await listar.Handle(new ListarLancamentosQuery(de, ate, tipo, pageSize, cursor), cancellationToken);
        return this.ToActionResult(result, Ok);
    }

    private string LocationOf(Guid id) => Url.Action(nameof(ObterPorId), new { id })!;
}
