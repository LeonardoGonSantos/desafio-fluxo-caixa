using FluxoCaixa.BuildingBlocks.Results;
using Microsoft.AspNetCore.Mvc;

namespace FluxoCaixa.BuildingBlocks.Api;

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

        // Erros de validação com detalhe por campo seguem o padrão ValidationProblemDetails (campo "errors").
        ProblemDetails problem = error.Details is { Count: > 0 }
            ? new ValidationProblemDetails(error.Details.ToDictionary(d => d.Key, d => d.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = title;
        problem.Detail = error.Message;
        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}
