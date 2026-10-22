using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lancamentos.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace Lancamentos.Api.Filters;

/// <summary>
/// Exige o header Idempotency-Key. Repetições com o mesmo corpo devolvem a resposta original
/// sem reexecutar a operação; o mesmo header com outro corpo é rejeitado (422).
/// </summary>
public sealed class IdempotencyFilter(IIdempotencyStore store, IOptions<MvcJsonOptions> jsonOptions)
    : IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    private const int MaxKeyLength = 100;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
        {
            context.Result = Problem(StatusCodes.Status400BadRequest, "Idempotency-Key inválida",
                $"O header {HeaderName} é obrigatório e deve ter até {MaxKeyLength} caracteres.", "idempotency.key_invalida");
            return;
        }

        var cancellationToken = context.HttpContext.RequestAborted;
        var requestHash = ComputeHash(context.ActionArguments.Values.Where(v => v is not CancellationToken));
        var reservation = await store.ReserveAsync(key, requestHash, cancellationToken);

        switch (reservation.Outcome)
        {
            case ReservationOutcome.Completed:
                context.HttpContext.Response.Headers["Idempotent-Replayed"] = "true";
                if (reservation.Response!.Location is { } location)
                {
                    context.HttpContext.Response.Headers.Location = location;
                }

                context.Result = new ContentResult
                {
                    StatusCode = reservation.Response.StatusCode,
                    Content = reservation.Response.Body,
                    ContentType = "application/json",
                };
                return;

            case ReservationOutcome.InProgress:
                context.Result = Problem(StatusCodes.Status409Conflict, "Requisição em andamento",
                    "Outra requisição com a mesma Idempotency-Key ainda está sendo processada.", "idempotency.em_andamento");
                return;

            case ReservationOutcome.HashMismatch:
                context.Result = Problem(StatusCodes.Status422UnprocessableEntity, "Idempotency-Key reutilizada",
                    "Esta Idempotency-Key já foi usada com um corpo de requisição diferente.", "idempotency.corpo_diferente");
                return;
        }

        var executed = await next();

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            await store.ReleaseAsync(key, CancellationToken.None);
            return;
        }

        if (executed.Result is ObjectResult { StatusCode: >= 200 and < 300 } objectResult)
        {
            var location = (objectResult as CreatedResult)?.Location;
            var body = JsonSerializer.Serialize(objectResult.Value, jsonOptions.Value.JsonSerializerOptions);
            await store.CompleteAsync(
                key, new IdempotentResponse(objectResult.StatusCode.Value, body, location), CancellationToken.None);
        }
        else
        {
            await store.ReleaseAsync(key, CancellationToken.None);
        }
    }

    private string ComputeHash(IEnumerable<object?> arguments)
    {
        var json = JsonSerializer.Serialize(arguments, jsonOptions.Value.JsonSerializerOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static ObjectResult Problem(int status, string title, string detail, string code)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}
