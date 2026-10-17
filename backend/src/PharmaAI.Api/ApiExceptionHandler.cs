using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Application.Common;
using PharmaAI.Domain.Common;

namespace PharmaAI.Api;

/// <summary>
/// Maps expected failures to RFC 7807 responses: validation → 400, missing entity → 404,
/// business-rule violation or concurrent edit → 409. Domain errors carry a stable "code"
/// extension the UI translates into Arabic.
/// </summary>
internal sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        IResult? result = exception switch
        {
            ValidationException validation => Results.ValidationProblem(
                validation.Errors
                    .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                extensions: CodeExtension(validation.Errors.Select(e => e.ErrorCode).FirstOrDefault(c => c.Contains('.')))),
            NotFoundException notFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not found",
                detail: notFound.Message),
            DomainException domain => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Business rule violated",
                detail: domain.Message,
                extensions: CodeExtension(domain.Code)),
            DuplicateKeyException => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Duplicate",
                detail: exception.Message,
                extensions: CodeExtension("conflict.duplicate")),
            DbUpdateConcurrencyException => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Concurrent update",
                detail: "The stock changed while saving. Please retry.",
                extensions: CodeExtension("stock.concurrent_update")),
            _ => null,
        };

        if (result is null)
        {
            return false;
        }

        await result.ExecuteAsync(httpContext);
        return true;
    }

    private static Dictionary<string, object?>? CodeExtension(string? code) =>
        code is null ? null : new Dictionary<string, object?> { ["code"] = code };
}
