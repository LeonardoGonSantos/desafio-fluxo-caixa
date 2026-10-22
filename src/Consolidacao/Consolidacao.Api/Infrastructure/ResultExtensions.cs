using FluxoCaixa.BuildingBlocks.Results;
using Microsoft.AspNetCore.Mvc;

namespace Consolidacao.Api.Infrastructure;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller, Result<T> result, Func<T, IActionResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : controller.ToProblem(result.Error);

    public static IActionResult ToProblem(this ControllerBase controller, Error error)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Requisição inválida"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflito de estado"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno"),
        };

        var problem = new ProblemDetails { Status = status, Title = title, Detail = error.Message };
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}
