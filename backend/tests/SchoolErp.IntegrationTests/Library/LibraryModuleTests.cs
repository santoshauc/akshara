using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolErp.Application;
using SchoolErp.Application.Abstractions;
using SchoolErp.Application.Academics;
using SchoolErp.Application.Common.Exceptions;
using SchoolErp.Application.Hostel;
using SchoolErp.Application.Library;
using SchoolErp.Application.Students;
using SchoolErp.Application.Students.Commands;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.TenantCatalog;
using SchoolErp.Infrastructure;
using SchoolErp.Infrastructure.Persistence;
using SchoolErp.Infrastructure.Tenancy;
using SchoolErp.IntegrationTests.Tenancy;
using Testcontainers.PostgreSql;

namespace SchoolErp.IntegrationTests.Library;

/// <summary>One school with two enrolled students for loan/stay scenarios.</summary>
public sealed class LibraryHostelFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("schoolerp_lib_test")
        .WithUsername("admin")
        .WithPassword("admin_test_pw")
        .Build();

    private ServiceProvider _provider = null!;

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid StudentA { get; private set; }

    public Guid StudentB { get; private set; }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _container.GetConnectionString(),
                ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789abcdef",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUser, StubCurrentUser>();
        _provider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "LIB001",
                Name = "Library Test School",
                Subdomain = "libtest",
                Status = TenantStatus.Active,
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new CreateAcademicYearCommand(
                "2026-27", new DateOnly(2026, 6, 1), new DateOnly(2027, 4, 30), MakeCurrent: true));
            var yearId = (await sender.Send(new GetAcademicYearsQuery())).Single().Id;
            var grade7 = await sender.Send(new CreateClassCommand("Grade 7", 7, ["A"]));
            var sectionId = grade7.Sections.Single().Id;

            StudentA = await sender.Send(new AdmitStudentCommand(
                null, "Ravi", "Verma", new DateOnly(2013, 4, 4), Gender.Male,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), yearId, grade7.Id, sectionId, 1,
                [new GuardianInput("Parent", "Verma", GuardianRelation.Father, "+919400000001", null, null, true)]));
            StudentB = await sender.Send(new AdmitStudentCommand(
                null, "Sita", "Rao", new DateOnly(2013, 5, 5), Gender.Female,
                null, null, null, null, null, null, null, null,
                new DateOnly(2026, 6, 5), yearId, grade7.Id, sectionId, 2,
                [new GuardianInput("Parent", "Rao", GuardianRelation.Mother, "+919400000002", null, null, true)]));
        }
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _container.DisposeAsync();
    }

    public AsyncServiceScope CreateScope()
    {
        var scope = _provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(TenantId);
        return scope;
    }
}

/// <summary>Library availability and fairness rules through the full pipeline.</summary>
public sealed class LibraryModuleTests : IClassFixture<LibraryHostelFixture>
{
    private readonly LibraryHostelFixture _fixture;

    public LibraryModuleTests(LibraryHostelFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Issue_and_return_track_availability_and_reject_exhausted_titles()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var bookId = await sender.Send(new AddBookCommand(
            "Wings of Fire", "A.P.J. Abdul Kalam", "9788173711466", "Biography", Copies: 1));

        var loanId = await sender.Send(new IssueBookCommand(bookId, _fixture.StudentA));
        (await sender.Send(new GetBooksQuery("Wings")))
            .Single().CopiesAvailable.Should().Be(0);

        // The single copy is out — the next student must wait.
        var exhausted = () => sender.Send(new IssueBookCommand(bookId, _fixture.StudentB));
        await exhausted.Should().ThrowAsync<ConflictException>().WithMessage("*on the shelf*");

        await sender.Send(new ReturnBookCommand(loanId));
        (await sender.Send(new GetBooksQuery("Wings")))
            .Single().CopiesAvailable.Should().Be(1);

        // Double-return is a conflict, not a silent success.
        var again = () => sender.Send(new ReturnBookCommand(loanId));
        await again.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task A_student_carries_at_most_three_open_loans_and_never_duplicates_a_title()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var first = await sender.Send(new AddBookCommand("Book One", "Author", null, null, 2));
        await sender.Send(new IssueBookCommand(first, _fixture.StudentB));

        var duplicate = () => sender.Send(new IssueBookCommand(first, _fixture.StudentB));
        await duplicate.Should().ThrowAsync<ConflictException>().WithMessage("*already has a copy*");

        for (var i = 2; i <= 3; i++)
        {
            var next = await sender.Send(new AddBookCommand($"Book {i}", "Author", null, null, 2));
            await sender.Send(new IssueBookCommand(next, _fixture.StudentB));
        }

        var fourth = await sender.Send(new AddBookCommand("Book Four", "Author", null, null, 2));
        var limit = () => sender.Send(new IssueBookCommand(fourth, _fixture.StudentB));
        await limit.Should().ThrowAsync<ConflictException>().WithMessage("*3 books*");

        // Overdue reporting: backdate a due date and check the flag.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var loan = await db.BookLoans.FirstAsync(l => l.StudentId == _fixture.StudentB);
        loan.DueOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        await db.SaveChangesAsync();

        (await sender.Send(new GetLoansQuery(OverdueOnly: true)))
            .Should().Contain(l => l.StudentId == _fixture.StudentB && l.Overdue);
    }
}

/// <summary>Hostel capacity and single-stay rules through the full pipeline.</summary>
public sealed class HostelModuleTests : IClassFixture<LibraryHostelFixture>
{
    private readonly LibraryHostelFixture _fixture;

    public HostelModuleTests(LibraryHostelFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Allocation_respects_capacity_single_stay_and_vacate_frees_the_bed()
    {
        await using var scope = _fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var hostelId = await sender.Send(new CreateHostelCommand(
            "Boys Hostel A", "Mr. Sharma", "+919500000001"));
        var roomId = await sender.Send(new AddHostelRoomCommand(hostelId, "101", Capacity: 1));

        var allocationId = await sender.Send(
            new AllocateHostelRoomCommand(roomId, _fixture.StudentA));

        // The single bed is taken.
        var full = () => sender.Send(new AllocateHostelRoomCommand(roomId, _fixture.StudentB));
        await full.Should().ThrowAsync<ConflictException>().WithMessage("*full*");

        // The resident can't be double-housed either.
        var secondRoom = await sender.Send(new AddHostelRoomCommand(hostelId, "102", Capacity: 2));
        var doubleStay = () => sender.Send(new AllocateHostelRoomCommand(secondRoom, _fixture.StudentA));
        await doubleStay.Should().ThrowAsync<ConflictException>().WithMessage("*already has a hostel room*");

        // Parents see the stay with the warden contact.
        var stay = await sender.Send(new GetStudentHostelQuery(_fixture.StudentA));
        stay.Should().NotBeNull();
        stay!.HostelName.Should().Be("Boys Hostel A");
        stay.RoomNumber.Should().Be("101");
        stay.WardenPhone.Should().Be("+919500000001");

        // Occupancy tallies agree.
        (await sender.Send(new GetHostelsQuery()))
            .Single(h => h.Id == hostelId).Occupied.Should().Be(1);

        // Vacate → the bed frees up and the parent view goes empty.
        await sender.Send(new VacateHostelRoomCommand(allocationId));
        (await sender.Send(new GetStudentHostelQuery(_fixture.StudentA))).Should().BeNull();
        await sender.Send(new AllocateHostelRoomCommand(roomId, _fixture.StudentB));
        (await sender.Send(new GetHostelRoomsQuery(hostelId)))
            .Single(r => r.Id == roomId).Occupied.Should().Be(1);
    }
}
