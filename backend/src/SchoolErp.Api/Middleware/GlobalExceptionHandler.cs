using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Common.Exceptions;

namespace SchoolErp.Api.Middleware;

/// <summary>
/// Maps the exceptions the application throws on purpose — a failed validator,
/// a missing record, a conflicting write — onto RFC 7807 problem responses.
/// Anything not listed here is a genuine fault and is left alone to become a
/// 500; internals are never leaked to the client.
/// </summary>
public static class ApplicationExceptionMapper
{
    /// <summary>The problem response for an expected exception, or null if it isn't one.</summary>
    public static ProblemDetails? TryMap(Exception exception) => exception switch
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
}

/// <summary>
/// Turns expected exceptions into responses INSIDE the MVC filter pipeline,
/// which is where essentially all of them are thrown.
/// <para>
/// This exists rather than leaving the work to <see cref="GlobalExceptionHandler"/>
/// because ASP.NET's exception-handler middleware logs "An unhandled exception
/// has occurred" at Error — with a full stack trace — BEFORE it consults any
/// <see cref="IExceptionHandler"/>. So every rejected form field and every
/// 404 lookup wrote a fault-sized entry to the log while the caller correctly
/// received 400 or 404, and real faults drowned in it. .NET 8 offers no hook
/// to suppress that, so the fix is to keep expected exceptions from reaching
/// the middleware at all. Genuine faults still pass straight through to it.
/// </para>
/// </summary>
public sealed class ApplicationExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (ApplicationExceptionMapper.TryMap(context.Exception) is not { } problem)
        {
            return; // a real fault: let it bubble to the middleware, logged in full
        }

        // ObjectResult serializes by RUNTIME type, so ValidationProblemDetails
        // keeps its per-field "errors" — declaring the variable as
        // ProblemDetails would silently drop them.
        context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
        context.ExceptionHandled = true;
    }
}

/// <summary>
/// The same mapping for exceptions thrown OUTSIDE the MVC pipeline (middleware,
/// endpoint routing). Rare, so the extra Error log entry the framework writes
/// before reaching here is not worth engineering around.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (ApplicationExceptionMapper.TryMap(exception) is not { } problem)
        {
            return false; // fall through to the default 500 handler
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        // Serialized as object so the runtime type wins — see the filter above.
        await httpContext.Response
            .WriteAsJsonAsync<object>(problem, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}
