using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Common.Exceptions;

namespace SchoolErp.Api.Middleware;

/// <summary>
/// Maps application exceptions to RFC 7807 problem responses. Anything not
/// listed here is a genuine 500 and is logged by Serilog upstream; internals
/// are never leaked to the client.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
            },

            NotFoundException notFound => new ProblemDetails
            {
                Title = notFound.Message,
                Status = StatusCodes.Status404NotFound,
            },

            ConflictException conflict => new ProblemDetails
            {
                Title = conflict.Message,
                Status = StatusCodes.Status409Conflict,
            },

            DbUpdateConcurrencyException => new ProblemDetails
            {
                Title = "The record was modified by someone else. Reload and try again.",
                Status = StatusCodes.Status409Conflict,
            },

            _ => null,
        };

        if (problem is null)
        {
            return false; // fall through to the default 500 handler
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
