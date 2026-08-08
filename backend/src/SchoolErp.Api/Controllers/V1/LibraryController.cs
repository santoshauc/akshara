using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Application.Library;
using SchoolErp.Shared.Authorization;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Library: catalog, issuing and returns.</summary>
[ApiController]
[RequiresModule(TenantModules.Library)]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/library")]
public sealed class LibraryController : ControllerBase
{
    private readonly ISender _sender;

    public LibraryController(ISender sender) => _sender = sender;

    /// <summary>The catalog with live availability.</summary>
    [HttpGet("books")]
    [HasPermission(Permissions.Library.View)]
    [ProducesResponseType(typeof(IReadOnlyList<BookDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBooks([FromQuery] string? search, CancellationToken ct) =>
        Ok(await _sender.Send(new GetBooksQuery(search), ct));

    /// <summary>Adds a title.</summary>
    [HttpPost("books")]
    [HasPermission(Permissions.Library.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddBook([FromBody] AddBookCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetBooks), new { }, id);
    }

    /// <summary>Open loans (optionally only overdue), or one student's history.</summary>
    [HttpGet("loans")]
    [HasPermission(Permissions.Library.View)]
    [ProducesResponseType(typeof(IReadOnlyList<BookLoanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLoans(
        [FromQuery] Guid? studentId, [FromQuery] bool overdueOnly, CancellationToken ct) =>
        Ok(await _sender.Send(new GetLoansQuery(studentId, overdueOnly), ct));

    /// <summary>Issues a copy to a student.</summary>
    [HttpPost("loans")]
    [HasPermission(Permissions.Library.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IssueBook(
        [FromBody] IssueBookCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetLoans), new { }, id);
    }

    /// <summary>Closes a loan (the copy goes back on the shelf).</summary>
    [HttpPost("loans/{loanId:guid}/return")]
    [HasPermission(Permissions.Library.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReturnBook(Guid loanId, CancellationToken ct)
    {
        await _sender.Send(new ReturnBookCommand(loanId), ct);
        return NoContent();
    }
}
