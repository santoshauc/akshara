using SchoolErp.Domain.Common;
using SchoolErp.Domain.Students;

namespace SchoolErp.Domain.Library;

/// <summary>A title in the school library with its copy counts.</summary>
public class Book : TenantEntity
{
    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string? Isbn { get; set; }

    /// <summary>Shelf/category label, free text (e.g. "Fiction", "Science").</summary>
    public string? Category { get; set; }

    public int CopiesTotal { get; set; }

    /// <summary>Copies currently on the shelf. Kept consistent by issue/return.</summary>
    public int CopiesAvailable { get; set; }
}

/// <summary>
/// One copy issued to a student. Open loans have a null
/// <see cref="ReturnedOn"/>; overdue = past <see cref="DueOn"/> and open.
/// </summary>
public class BookLoan : TenantEntity
{
    public Guid BookId { get; set; }

    public Book? Book { get; set; }

    public Guid StudentId { get; set; }

    public Student? Student { get; set; }

    public DateOnly IssuedOn { get; set; }

    public DateOnly DueOn { get; set; }

    public DateOnly? ReturnedOn { get; set; }
}
