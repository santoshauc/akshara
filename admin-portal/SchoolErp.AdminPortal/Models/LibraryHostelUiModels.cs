namespace SchoolErp.AdminPortal.Models;

/// <summary>Library title (mirrors BookDto).</summary>
public sealed record BookDto(
    Guid Id,
    string Title,
    string Author,
    string? Isbn,
    string? Category,
    int CopiesTotal,
    int CopiesAvailable);

/// <summary>Loan row (mirrors BookLoanDto).</summary>
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

/// <summary>Add-title payload (mirrors AddBookCommand).</summary>
public sealed record AddBookRequest(
    string Title, string Author, string? Isbn, string? Category, int Copies);

/// <summary>Issue payload (mirrors IssueBookCommand).</summary>
public sealed record IssueBookRequest(Guid BookId, Guid StudentId, int LoanDays);

/// <summary>Hostel row (mirrors HostelDto).</summary>
public sealed record HostelDto(
    Guid Id,
    string Name,
    string? WardenName,
    string? WardenPhone,
    int RoomCount,
    int Capacity,
    int Occupied);

/// <summary>Room row (mirrors HostelRoomDto).</summary>
public sealed record HostelRoomDto(
    Guid Id, Guid HostelId, string RoomNumber, int Capacity, int Occupied);

/// <summary>Stay row (mirrors HostelAllocationDto).</summary>
public sealed record HostelAllocationDto(
    Guid Id,
    Guid RoomId,
    string RoomNumber,
    string HostelName,
    Guid StudentId,
    string StudentName,
    string AdmissionNumber,
    DateOnly AllocatedOn,
    DateOnly? VacatedOn);

/// <summary>Create-hostel payload (mirrors CreateHostelCommand).</summary>
public sealed record CreateHostelRequest(string Name, string? WardenName, string? WardenPhone);

/// <summary>Add-room payload (mirrors AddRoomRequest).</summary>
public sealed record AddHostelRoomRequest(string RoomNumber, int Capacity);

/// <summary>Allocate payload (mirrors AllocateHostelRoomCommand).</summary>
public sealed record AllocateRequest(Guid RoomId, Guid StudentId);
