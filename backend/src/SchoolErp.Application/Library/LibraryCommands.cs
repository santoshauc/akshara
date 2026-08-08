using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Domain.Library;

namespace SchoolErp.Application.Library;

/// <summary>A library title with live availability.</summary>
public sealed record BookDto(
    Guid Id,
    string Title,
    string Author,
    string? Isbn,
    string? Category,
    int CopiesTotal,
    int CopiesAvailable);

/// <summary>One loan row as shown to staff (and parents for their child).</summary>
public sealed record BookLoanDto(
    Guid Id,
    Guid BookId,
    string BookTitle,
    string Author,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    DateOnly IssuedOn,
    DateOnly DueOn,
    DateOnly? ReturnedOn,
    bool Overdue);

/// <summary>Adds a title to the catalog.</summary>
public sealed record AddBookCommand(
    string Title, string Author, string? Isbn, string? Category, int Copies) : IRequest<Guid>;

/// <summary>Catalog shape rules.</summary>
public sealed class AddBookCommandValidator : AbstractValidator<AddBookCommand>
{
    public AddBookCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Author).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Isbn).MaximumLength(20);
        RuleFor(c => c.Category).MaximumLength(64);
        RuleFor(c => c.Copies).InclusiveBetween(1, 1000);
    }
}

/// <summary>All copies start on the shelf.</summary>
public sealed class AddBookCommandHandler : IRequestHandler<AddBookCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public AddBookCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(AddBookCommand request, CancellationToken cancellationToken)
    {
        var book = new Book
        {
            Title = request.Title.Trim(),
            Author = request.Author.Trim(),
            Isbn = string.IsNullOrWhiteSpace(request.Isbn) ? null : request.Isbn.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            CopiesTotal = request.Copies,
            CopiesAvailable = request.Copies,
        };
        _db.Books.Add(book);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return book.Id;
    }
}

/// <summary>The catalog, optionally filtered by title/author/category.</summary>
public sealed record GetBooksQuery(string? Search = null) : IRequest<IReadOnlyList<BookDto>>;

/// <summary>Ordered by title.</summary>
public sealed class GetBooksQueryHandler : IRequestHandler<GetBooksQuery, IReadOnlyList<BookDto>>
{
    private readonly IApplicationDbContext _db;

    public GetBooksQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<BookDto>> Handle(
        GetBooksQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Books.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(b =>
                EF.Functions.ILike(b.Title, $"%{term}%") ||
                EF.Functions.ILike(b.Author, $"%{term}%") ||
                (b.Category != null && EF.Functions.ILike(b.Category, $"%{term}%")));
        }

        return await query
            .OrderBy(b => b.Title)
            .Select(b => new BookDto(
                b.Id, b.Title, b.Author, b.Isbn, b.Category, b.CopiesTotal, b.CopiesAvailable))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>Issues a copy to a student for <paramref name="LoanDays"/> days.</summary>
public sealed record IssueBookCommand(Guid BookId, Guid StudentId, int LoanDays = 14) : IRequest<Guid>;

/// <summary>Loan shape rules.</summary>
public sealed class IssueBookCommandValidator : AbstractValidator<IssueBookCommand>
{
    public IssueBookCommandValidator()
    {
        RuleFor(c => c.LoanDays).InclusiveBetween(1, 90);
    }
}

/// <summary>
/// Availability and fairness rules: a copy must be on the shelf, a student
/// carries at most 3 open loans and never two copies of the same title.
/// </summary>
public sealed class IssueBookCommandHandler : IRequestHandler<IssueBookCommand, Guid>
{
    internal const int MaxOpenLoans = 3;

    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public IssueBookCommandHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Guid> Handle(IssueBookCommand request, CancellationToken cancellationToken)
    {
        var book = await _db.Books
            .FirstOrDefaultAsync(b => b.Id == request.BookId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(Book), request.BookId);

        if (!await _db.Students.AnyAsync(s => s.Id == request.StudentId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new NotFoundException("Student", request.StudentId);
        }

        if (book.CopiesAvailable <= 0)
        {
            throw new ConflictException($"No copies of '{book.Title}' are on the shelf.");
        }

        var openLoans = await _db.BookLoans
            .Where(l => l.StudentId == request.StudentId && l.ReturnedOn == null)
            .Select(l => l.BookId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (openLoans.Count >= MaxOpenLoans)
        {
            throw new ConflictException(
                $"The student already has {MaxOpenLoans} books out. Return one first.");
        }

        if (openLoans.Contains(book.Id))
        {
            throw new ConflictException(
                $"The student already has a copy of '{book.Title}'.");
        }

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var loan = new BookLoan
        {
            BookId = book.Id,
            StudentId = request.StudentId,
            IssuedOn = today,
            DueOn = today.AddDays(request.LoanDays),
        };
        book.CopiesAvailable--;
        _db.BookLoans.Add(loan);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return loan.Id;
    }
}

/// <summary>Returns a copy: closes the loan and puts it back on the shelf.</summary>
public sealed record ReturnBookCommand(Guid LoanId) : IRequest;

/// <summary>Idempotence: returning an already-closed loan is a 409.</summary>
public sealed class ReturnBookCommandHandler : IRequestHandler<ReturnBookCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public ReturnBookCommandHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task Handle(ReturnBookCommand request, CancellationToken cancellationToken)
    {
        var loan = await _db.BookLoans
            .Include(l => l.Book)
            .FirstOrDefaultAsync(l => l.Id == request.LoanId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(nameof(BookLoan), request.LoanId);

        if (loan.ReturnedOn is not null)
        {
            throw new ConflictException("This loan is already closed.");
        }

        loan.ReturnedOn = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        loan.Book!.CopiesAvailable = Math.Min(loan.Book.CopiesTotal, loan.Book.CopiesAvailable + 1);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Loans for staff: open loans by default (optionally only overdue ones), or
/// one student's full history.
/// </summary>
public sealed record GetLoansQuery(Guid? StudentId = null, bool OverdueOnly = false)
    : IRequest<IReadOnlyList<BookLoanDto>>;

/// <summary>Newest first; overdue computed against today.</summary>
public sealed class GetLoansQueryHandler : IRequestHandler<GetLoansQuery, IReadOnlyList<BookLoanDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public GetLoansQueryHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<BookLoanDto>> Handle(
        GetLoansQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var query = _db.BookLoans.AsNoTracking();

        query = request.StudentId is { } studentId
            ? query.Where(l => l.StudentId == studentId)
            : query.Where(l => l.ReturnedOn == null);
        if (request.OverdueOnly)
        {
            query = query.Where(l => l.ReturnedOn == null && l.DueOn < today);
        }

        return await query
            .OrderByDescending(l => l.IssuedOn)
            .Select(l => new BookLoanDto(
                l.Id,
                l.BookId,
                l.Book!.Title,
                l.Book.Author,
                l.StudentId,
                (l.Student!.FirstName + " " + l.Student.LastName).Trim(),
                l.Student.AdmissionNumber,
                l.IssuedOn,
                l.DueOn,
                l.ReturnedOn,
                l.ReturnedOn == null && l.DueOn < today))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
