using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace PharmaAI.Api;

/// <summary>Turns FluentValidation failures from the MediatR pipeline into RFC 7807 400 responses.</summary>
internal sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validation)
        {
            return false;
        }

        var errors = validation.Errors
            .GroupBy(e => char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        await Results.ValidationProblem(errors).ExecuteAsync(httpContext);
        return true;
    }
}
