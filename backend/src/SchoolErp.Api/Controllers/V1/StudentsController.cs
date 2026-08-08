using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolErp.Api.Authorization;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Application.Students.Queries;
using SchoolErp.Domain.Students;
using SchoolErp.Shared.Authorization;
using SchoolErp.Shared.Models;

namespace SchoolErp.Api.Controllers.V1;

/// <summary>Student information system: admissions and student records.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/students")]
public sealed class StudentsController : ControllerBase
{
    private readonly ISender _sender;

    public StudentsController(ISender sender) => _sender = sender;

    /// <summary>Paged student list with search and placement filters.</summary>
    [HttpGet]
    [HasPermission(Permissions.Students.View)]
    [ProducesResponseType(typeof(PagedResult<StudentListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudents(
        [FromQuery] string? search,
        [FromQuery] Guid? academicYearId,
        [FromQuery] Guid? classId,
        [FromQuery] Guid? sectionId,
        [FromQuery] StudentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetStudentsQuery(search, academicYearId, classId, sectionId, status, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Full student detail with guardians and current placement.</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Students.View)]
    [ProducesResponseType(typeof(StudentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudent(Guid id, CancellationToken ct) =>
        Ok(await _sender.Send(new GetStudentByIdQuery(id), ct));

    /// <summary>Admits a student with guardians and initial enrollment.</summary>
    [HttpPost]
    [HasPermission(Permissions.Students.Manage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdmitStudent(
        [FromBody] AdmitStudentCommand command, CancellationToken ct)
    {
        var id = await _sender.Send(command, ct);
        return CreatedAtAction(nameof(GetStudent), new { id, version = "1" }, id);
    }

    /// <summary>
    /// An official document PDF: transfer-certificate, bonafide-certificate
    /// or id-card.
    /// </summary>
    [HttpGet("{id:guid}/documents/{type}")]
    [HasPermission(Permissions.Students.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocument(Guid id, string type, CancellationToken ct)
    {
        StudentDocumentType? documentType = type.ToLowerInvariant() switch
        {
            "transfer-certificate" => StudentDocumentType.TransferCertificate,
            "bonafide-certificate" => StudentDocumentType.BonafideCertificate,
            "id-card" => StudentDocumentType.IdCard,
            _ => null,
        };
        if (documentType is null)
        {
            return NotFound();
        }

        var pdf = await _sender.Send(
            new GetStudentDocumentPdfQuery(id, documentType.Value), ct);
        return File(pdf, "application/pdf", $"{type}.pdf");
    }

    /// <summary>Uploads/replaces the student photo (jpeg/png/webp, max 2 MB).</summary>
    [HttpPost("{id:guid}/photo")]
    [HasPermission(Permissions.Students.Manage)]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadPhoto(
        Guid id, IFormFile file, [FromServices] IFileStorage storage, CancellationToken ct)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length == 0 || extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            return Problem(
                title: "Upload a .jpg, .png or .webp image (max 2 MB).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        await using var content = file.OpenReadStream();
        var key = await storage.SaveAsync("student-photos", extension, content, ct);
        var photoUrl = $"/api/v1/files/{key}";

        var previousUrl = await _sender.Send(new SetStudentPhotoCommand(id, photoUrl), ct);
        if (previousUrl is not null &&
            previousUrl.StartsWith("/api/v1/files/", StringComparison.Ordinal))
        {
            await storage.DeleteAsync(previousUrl["/api/v1/files/".Length..], ct);
        }

        return Ok(photoUrl);
    }
}
