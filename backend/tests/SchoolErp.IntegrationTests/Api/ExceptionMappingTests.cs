using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Api.Middleware;
using SchoolErp.Application.Common.Exceptions;

namespace SchoolErp.IntegrationTests.Api;

/// <summary>
/// Expected exceptions must become problem responses inside the MVC filter, so
/// they never reach the exception-handler middleware — which logs whatever it
/// sees as a fault, stack trace and all, before consulting any handler. These
/// tests need no database; they guard the pipeline contract itself.
/// </summary>
public sealed class ExceptionMappingTests
{
    private static ExceptionContext ContextFor(Exception exception) =>
        new(
            new ActionContext(
                new DefaultHttpContext(),
                new RouteData(),
                new ActionDescriptor()),
            [])
        {
            Exception = exception,
        };

    private static ExceptionContext Run(Exception exception)
    {
        var context = ContextFor(exception);
        new ApplicationExceptionFilter().OnException(context);
        return context;
    }

    [Fact]
    public void A_failed_validator_becomes_a_400_that_still_names_the_fields()
    {
        var failures = new[]
        {
            new ValidationFailure("Entries", "'Entries' must not be empty."),
            new ValidationFailure("Date", "'Date' is required."),
        };

        var context = Run(new ValidationException(failures));

        context.ExceptionHandled.Should().BeTrue();
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        // Declaring the value as ProblemDetails would serialize away the
        // per-field errors — the client would get a bare title.
        var problem = result.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey("Entries");
        problem.Errors["Entries"].Should().Contain("'Entries' must not be empty.");
        problem.Errors.Should().ContainKey("Date");
    }

    [Fact]
    public void A_missing_record_becomes_a_404_carrying_what_was_not_found()
    {
        var context = Run(new NotFoundException("Student", Guid.Empty));

        context.ExceptionHandled.Should().BeTrue();
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        result.Value.Should().BeOfType<ProblemDetails>()
            .Which.Title.Should().Contain("Student");
    }

    [Fact]
    public void A_conflicting_write_becomes_a_409()
    {
        var context = Run(new ConflictException("That admission number is taken."));

        context.ExceptionHandled.Should().BeTrue();
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        result.Value.Should().BeOfType<ProblemDetails>()
            .Which.Title.Should().Be("That admission number is taken.");
    }

    [Fact]
    public void A_concurrent_write_reads_as_a_conflict_the_operator_can_act_on()
    {
        var context = Run(new DbUpdateConcurrencyException());

        context.ExceptionHandled.Should().BeTrue();
        var problem = context.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Contain("Reload");
    }

    [Fact]
    public void A_genuine_fault_is_left_alone_so_it_is_still_logged_and_500s()
    {
        var context = Run(new InvalidOperationException("something actually broke"));

        context.ExceptionHandled.Should().BeFalse("a real fault must reach the middleware");
        context.Result.Should().BeNull();
    }

    [Fact]
    public void The_middleware_handler_maps_the_same_exceptions_as_the_filter()
    {
        // The two entry points must not drift: the handler is the net for
        // exceptions thrown outside MVC.
        ApplicationExceptionMapper.TryMap(new NotFoundException("Student", Guid.Empty))
            .Should().NotBeNull();
        ApplicationExceptionMapper.TryMap(new InvalidOperationException("boom"))
            .Should().BeNull();
    }
}
