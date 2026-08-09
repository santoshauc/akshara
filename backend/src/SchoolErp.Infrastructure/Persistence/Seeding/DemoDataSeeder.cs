using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolErp.Application.Abstractions;
using SchoolErp.Domain.Academics;
using SchoolErp.Domain.Admissions;
using SchoolErp.Domain.Attendance;
using SchoolErp.Domain.Communication;
using SchoolErp.Domain.Exams;
using SchoolErp.Domain.Fees;
using SchoolErp.Domain.Homework;
using SchoolErp.Domain.Leave;
using SchoolErp.Domain.Library;
using SchoolErp.Domain.Staff;
using SchoolErp.Domain.Students;
using SchoolErp.Domain.Timetable;
using SchoolErp.Domain.Transport;

namespace SchoolErp.Infrastructure.Persistence.Seeding;

/// <summary>
/// Fills the demo school with a realistic, demo-ready dataset: two full
/// sections of students, a month of attendance, two published exams with
/// marks, fee collections spread over months, a complete timetable, an
/// admissions pipeline, and enough library/homework/notice content that every
/// screen and chart has something to show. Idempotent — each block checks
/// before inserting, so it can run at every dev startup. Never wired in
/// production (invoked from DevSeeder only).
/// </summary>
public static partial class DemoDataSeeder
{
    /// <summary>Names for Grade 5 A; guardian surnames match.</summary>
    private static readonly (string First, string Last, Gender Gender)[] Grade5Names =
    [
        ("Advika", "Rao", Gender.Female), ("Bhavesh", "Naidu", Gender.Male),
        ("Chandini", "Sree", Gender.Female), ("Dhruv", "Varma", Gender.Male),
        ("Esha", "Kapoor", Gender.Female), ("Farhan", "Ali", Gender.Male),
        ("Gauri", "Pillai", Gender.Female), ("Harsha", "Vardhan", Gender.Male),
    ];

    private static readonly (string First, string Last, Gender Gender)[] Grade6Names =
    [
        ("Ishita", "Reddy", Gender.Female), ("Jayanth", "Kumar", Gender.Male),
        ("Kavya", "Nair", Gender.Female), ("Lakshman", "Das", Gender.Male),
        ("Mounika", "Devi", Gender.Female), ("Nikhil", "Joshi", Gender.Male),
        ("Pallavi", "Singh", Gender.Female), ("Rithvik", "Chowdary", Gender.Male),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DemoDataSeeder");

        var demo = await db.Tenants
            .FirstOrDefaultAsync(t => t.Code == DevSeeder.DemoSchoolCode)
            .ConfigureAwait(false);
        if (demo is null)
        {
            return; // fresh database — the shell seed runs first; we enrich next startup
        }

        // A distinctive default theme so per-school branding is visible in demos.
        if (demo.ThemePrimaryColor is null)
        {
            demo.ThemePrimaryColor = "#00695C";
            demo.ThemeSecondaryColor = "#FF8F00";
            await db.SaveChangesAsync().ConfigureAwait(false);
        }

        scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().SetTenant(demo.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var year = await EnsureAcademicSetupAsync(db).ConfigureAwait(false);
        var subjects = await EnsureSubjectsAsync(db).ConfigureAwait(false);
        var teachers = await EnsureTeachersAsync(db).ConfigureAwait(false);

        var classes = await db.SchoolClasses
            .Include(c => c.Sections)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync()
            .ConfigureAwait(false);
        var grade5 = classes.FirstOrDefault(c => c.Name == "Grade 5");
        var grade6 = classes.FirstOrDefault(c => c.Name == "Grade 6");
        if (grade5?.Sections.FirstOrDefault() is not { } section5 ||
            grade6?.Sections.FirstOrDefault() is not { } section6)
        {
            return; // classes exist in every set-up demo db; bail quietly otherwise
        }

        var seededStudents = await EnsureStudentsAsync(
            db, year, grade5, section5, grade6, section6).ConfigureAwait(false);
        await EnsureAttendanceAsync(db, year, today).ConfigureAwait(false);
        await EnsureExamsAndMarksAsync(db, year, [grade5.Id, grade6.Id], subjects)
            .ConfigureAwait(false);
        await EnsureFeesAsync(db, year, grade5.Id, grade6.Id, today).ConfigureAwait(false);
        await EnsureTimetableAsync(db, grade5.Id, grade6.Id, subjects, teachers)
            .ConfigureAwait(false);
        await EnsureSubstitutionsAsync(db, teachers, today).ConfigureAwait(false);
        await EnsureEnquiriesAsync(db, today).ConfigureAwait(false);
        await EnsureCommunicationAsync(db, grade5.Id, subjects, today).ConfigureAwait(false);
        await EnsureLibraryAsync(db, today).ConfigureAwait(false);
        await EnsureLeaveAsync(db, today).ConfigureAwait(false);
        await EnsureTransportAsync(db).ConfigureAwait(false);

        if (seededStudents > 0)
        {
            LogSeeded(logger, seededStudents);
        }
    }

    // --- academic shell ----------------------------------------------------

    private static async Task<AcademicYear> EnsureAcademicSetupAsync(AppDbContext db)
    {
        var year = await db.AcademicYears.FirstOrDefaultAsync(y => y.IsCurrent).ConfigureAwait(false);
        if (year is null)
        {
            year = new AcademicYear
            {
                Name = "2026-27",
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2027, 4, 30),
                IsCurrent = true,
            };
            db.AcademicYears.Add(year);
        }

        foreach (var (name, order) in new[] { ("Grade 5", 5), ("Grade 6", 6) })
        {
            if (!await db.SchoolClasses.AnyAsync(c => c.Name == name).ConfigureAwait(false))
            {
                db.SchoolClasses.Add(new SchoolClass
                {
                    Name = name,
                    DisplayOrder = order,
                    Sections = [new Section { Name = "A", Capacity = 40 }],
                });
            }
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
        return year;
    }

    private static async Task<Dictionary<string, Subject>> EnsureSubjectsAsync(AppDbContext db)
    {
        var wanted = new (string Name, string Code)[]
        {
            ("Mathematics", "MAT"), ("Science", "SCI"), ("English", "ENG"),
            ("Telugu", "TEL"), ("Social Studies", "SOC"),
        };
        var existing = await db.Subjects.ToListAsync().ConfigureAwait(false);
        foreach (var (name, code) in wanted)
        {
            if (!existing.Any(s => s.Name == name))
            {
                var subject = new Subject { Name = name, Code = code };
                db.Subjects.Add(subject);
                existing.Add(subject);
            }
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
        return existing.ToDictionary(s => s.Name);
    }

    private static async Task<Dictionary<string, Teacher>> EnsureTeachersAsync(AppDbContext db)
    {
        var wanted = new (string Code, string Name, string Phone)[]
        {
            ("EMP-001", "Anita Rao", "+919888811122"),
            ("EMP-002", "Vikram Sharma", "+919888822233"),
            ("EMP-003", "Sunita Devi", "+919888833344"),
            ("EMP-004", "Ravi Prasad", "+919888844455"),
        };
        var existing = await db.Teachers.ToListAsync().ConfigureAwait(false);
        foreach (var (code, name, phone) in wanted)
        {
            if (!existing.Any(t => t.EmployeeCode == code))
            {
                var teacher = new Teacher
                {
                    EmployeeCode = code,
                    FullName = name,
                    Phone = phone,
                    JoinedOn = new DateOnly(2024, 6, 1),
                    IsActive = true,
                };
                db.Teachers.Add(teacher);
                existing.Add(teacher);
            }
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
        return existing.ToDictionary(t => t.FullName);
    }

    // --- students ----------------------------------------------------------

    private static async Task<int> EnsureStudentsAsync(
        AppDbContext db, AcademicYear year,
        SchoolClass grade5, Section section5, SchoolClass grade6, Section section6)
    {
        // The block below has stable admission numbers, so presence of the
        // first one means the whole cohort is already in.
        if (await db.Students.AnyAsync(s => s.AdmissionNumber == "ADM-2026-0101")
                .ConfigureAwait(false))
        {
            return 0;
        }

        var added = 0;
        added += await AddCohortAsync(db, year, grade5.Id, section5.Id, Grade5Names,
            baseAdmission: 101, basePhone: 210, birthYear: 2016).ConfigureAwait(false);
        added += await AddCohortAsync(db, year, grade6.Id, section6.Id, Grade6Names,
            baseAdmission: 121, basePhone: 230, birthYear: 2015).ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return added;
    }

    private static async Task<int> AddCohortAsync(
        AppDbContext db, AcademicYear year, Guid classId, Guid sectionId,
        (string First, string Last, Gender Gender)[] names,
        int baseAdmission, int basePhone, int birthYear)
    {
        var maxRoll = await db.Enrollments
            .Where(e => e.SectionId == sectionId && e.AcademicYearId == year.Id)
            .MaxAsync(e => (int?)e.RollNumber)
            .ConfigureAwait(false) ?? 0;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < names.Length; i++)
        {
            var (first, last, gender) = names[i];
            var student = new Student
            {
                AdmissionNumber = $"ADM-2026-{baseAdmission + i:D4}",
                FirstName = first,
                LastName = last,
                // The first child of each cohort celebrates today — the
                // dashboard birthday card always has something to show.
                DateOfBirth = i == 0
                    ? new DateOnly(birthYear, today.Month, today.Day)
                    : new DateOnly(birthYear, 1 + (i * 5 % 12), 1 + (i * 7 % 27)),
                Gender = gender,
                City = "Hyderabad",
                State = "Telangana",
                AdmissionDate = new DateOnly(2026, 6, 5),
                Status = StudentStatus.Active,
            };
            db.Students.Add(student);

            var guardian = new Guardian
            {
                FirstName = gender == Gender.Female ? "Lakshmi" : "Suresh",
                LastName = last,
                Relation = gender == Gender.Female ? GuardianRelation.Mother : GuardianRelation.Father,
                Phone = $"+9198765002{basePhone + i - 200:D2}",
            };
            db.Guardians.Add(guardian);
            db.StudentGuardians.Add(new StudentGuardian
            {
                StudentId = student.Id,
                GuardianId = guardian.Id,
                IsPrimary = true,
            });

            db.Enrollments.Add(new Enrollment
            {
                StudentId = student.Id,
                AcademicYearId = year.Id,
                SchoolClassId = classId,
                SectionId = sectionId,
                RollNumber = maxRoll + i + 1,
                Status = EnrollmentStatus.Active,
            });
        }

        return names.Length;
    }

    // --- attendance --------------------------------------------------------

    private static async Task EnsureAttendanceAsync(AppDbContext db, AcademicYear year, DateOnly today)
    {
        var from = today.AddDays(-29) < year.StartDate ? year.StartDate : today.AddDays(-29);
        var enrollments = await db.Enrollments
            .Where(e => e.AcademicYearId == year.Id && e.Status == EnrollmentStatus.Active)
            .Select(e => new { e.Id, e.StudentId, e.SectionId })
            .ToListAsync()
            .ConfigureAwait(false);

        var existing = (await db.AttendanceRecords
                .Where(a => a.Period == null && a.Date >= from && a.Date <= today)
                .Select(a => new { a.StudentId, a.Date })
                .ToListAsync()
                .ConfigureAwait(false))
            .Select(a => (a.StudentId, a.Date))
            .ToHashSet();

        // Deterministic so re-runs and screenshots stay stable.
        var rng = new Random(20260809);
        var added = 0;
        for (var date = from; date <= today; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            foreach (var enrollment in enrollments)
            {
                if (existing.Contains((enrollment.StudentId, date)))
                {
                    continue;
                }

                var roll = rng.NextDouble();
                var status = roll < 0.90 ? AttendanceStatus.Present
                    : roll < 0.94 ? AttendanceStatus.Late
                    : roll < 0.99 ? AttendanceStatus.Absent
                    : AttendanceStatus.HalfDay;
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    EnrollmentId = enrollment.Id,
                    StudentId = enrollment.StudentId,
                    SectionId = enrollment.SectionId,
                    Date = date,
                    Status = status,
                });
                added++;
            }
        }

        if (added > 0)
        {
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    // --- exams -------------------------------------------------------------

    private static async Task EnsureExamsAndMarksAsync(
        AppDbContext db, AcademicYear year, Guid[] classIds,
        Dictionary<string, Subject> subjects)
    {
        var examSubjects = new[] { "Mathematics", "Science", "English" };

        var unitTest = await EnsureExamAsync(db, year, "Unit Test 1",
            new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 8)).ConfigureAwait(false);
        var midTerm = await EnsureExamAsync(db, year, "Mid-Term 1",
            new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 7)).ConfigureAwait(false);

        foreach (var (exam, maxMarks, passMarks) in new[] { (unitTest, 50m, 17m), (midTerm, 100m, 33m) })
        {
            foreach (var classId in classIds)
            {
                foreach (var subjectName in examSubjects)
                {
                    var subjectId = subjects[subjectName].Id;
                    if (!await db.ExamSubjects.AnyAsync(s =>
                            s.ExamId == exam.Id && s.SchoolClassId == classId &&
                            s.SubjectId == subjectId).ConfigureAwait(false))
                    {
                        db.ExamSubjects.Add(new ExamSubject
                        {
                            ExamId = exam.Id,
                            SchoolClassId = classId,
                            SubjectId = subjectId,
                            ExamDate = exam.StartDate,
                            MaxMarks = maxMarks,
                            PassMarks = passMarks,
                        });
                    }
                }
            }
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
        await FillMarksAsync(db, year).ConfigureAwait(false);
    }

    private static async Task<Exam> EnsureExamAsync(
        AppDbContext db, AcademicYear year, string name, DateOnly start, DateOnly end)
    {
        var exam = await db.Exams
            .FirstOrDefaultAsync(e => e.Name == name && e.AcademicYearId == year.Id)
            .ConfigureAwait(false);
        if (exam is null)
        {
            exam = new Exam
            {
                Name = name,
                AcademicYearId = year.Id,
                StartDate = start,
                EndDate = end,
                Status = ExamStatus.Published,
            };
            db.Exams.Add(exam);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        else if (exam.Status != ExamStatus.Published)
        {
            exam.Status = ExamStatus.Published;
            await db.SaveChangesAsync().ConfigureAwait(false);
        }

        return exam;
    }

    private static async Task FillMarksAsync(AppDbContext db, AcademicYear year)
    {
        var papers = await (
                from s in db.ExamSubjects
                join x in db.Exams on s.ExamId equals x.Id
                where x.AcademicYearId == year.Id
                select new { s.Id, s.SchoolClassId, s.SubjectId, s.MaxMarks })
            .ToListAsync()
            .ConfigureAwait(false);
        var enrollments = await db.Enrollments
            .Where(e => e.AcademicYearId == year.Id && e.Status == EnrollmentStatus.Active)
            .Select(e => new { e.Id, e.StudentId, e.SchoolClassId })
            .ToListAsync()
            .ConfigureAwait(false);
        var existing = (await db.MarkEntries
                .Select(m => new { m.ExamSubjectId, m.StudentId })
                .ToListAsync()
                .ConfigureAwait(false))
            .Select(m => (m.ExamSubjectId, m.StudentId))
            .ToHashSet();

        var added = 0;
        foreach (var paper in papers)
        {
            foreach (var enrollment in enrollments.Where(e => e.SchoolClassId == paper.SchoolClassId))
            {
                if (existing.Contains((paper.Id, enrollment.StudentId)))
                {
                    continue;
                }

                // Stable per-student ability with per-paper variation: 42–97 %.
                var ability = 52 + Math.Abs(enrollment.StudentId.GetHashCode()) % 41;
                var jitter = (Math.Abs(paper.SubjectId.GetHashCode() ^ enrollment.StudentId.GetHashCode()) % 17) - 8;
                var percent = Math.Clamp(ability + jitter, 42, 97);
                db.MarkEntries.Add(new MarkEntry
                {
                    ExamSubjectId = paper.Id,
                    EnrollmentId = enrollment.Id,
                    StudentId = enrollment.StudentId,
                    MarksObtained = Math.Round(paper.MaxMarks * percent / 100m, 0),
                });
                added++;
            }
        }

        if (added > 0)
        {
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    // --- fees --------------------------------------------------------------

    private static async Task EnsureFeesAsync(
        AppDbContext db, AcademicYear year, Guid grade5Id, Guid grade6Id, DateOnly today)
    {
        var tuition = await db.FeeHeads.FirstOrDefaultAsync(h => h.Name == "Tuition")
            .ConfigureAwait(false);
        if (tuition is null)
        {
            tuition = new FeeHead { Name = "Tuition" };
            db.FeeHeads.Add(tuition);
        }

        foreach (var (classId, amount, due) in new[]
                 {
                     (grade5Id, 30_000m, new DateOnly(2026, 9, 6)),
                     (grade6Id, 25_000m, new DateOnly(2026, 10, 6)),
                 })
        {
            if (!await db.FeeStructureItems.AnyAsync(i =>
                    i.AcademicYearId == year.Id && i.SchoolClassId == classId)
                .ConfigureAwait(false))
            {
                db.FeeStructureItems.Add(new FeeStructureItem
                {
                    AcademicYearId = year.Id,
                    SchoolClassId = classId,
                    FeeHeadId = tuition.Id,
                    Amount = amount,
                    DueDate = due,
                });
            }
        }

        await db.SaveChangesAsync().ConfigureAwait(false);

        // Payments for the seeded cohort only (ADM-2026-01xx), spread over the
        // months since the year opened so the collections chart has a shape.
        if (await db.FeePayments.AnyAsync(p => p.ReceiptNumber.StartsWith("RCP-2026-1"))
                .ConfigureAwait(false))
        {
            return;
        }

        var cohort = await (
                from s in db.Students
                where s.AdmissionNumber.StartsWith("ADM-2026-01")
                join e in db.Enrollments on s.Id equals e.StudentId
                where e.AcademicYearId == year.Id
                select new { s.Id, e.SchoolClassId })
            .OrderBy(s => s.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        var receipt = 1001;
        var concessionsAdded = 0;
        for (var i = 0; i < cohort.Count; i++)
        {
            var total = cohort[i].SchoolClassId == grade5Id ? 30_000m : 25_000m;
            switch (i % 3)
            {
                case 0: // fully paid in two installments
                    AddPayment(db, year.Id, cohort[i].Id, total / 2, new DateOnly(2026, 6, 10 + i % 15), ref receipt);
                    AddPayment(db, year.Id, cohort[i].Id, total / 2, new DateOnly(2026, 7, 5 + i % 20), ref receipt);
                    break;
                case 1: // part paid recently
                    AddPayment(db, year.Id, cohort[i].Id, Math.Round(total * 0.4m, 0),
                        new DateOnly(2026, 8, 1 + i % Math.Max(1, today.Day)), ref receipt);
                    break;
                default: // outstanding — nothing paid yet
                    break;
            }

            if (concessionsAdded < 2 && i % 5 == 4)
            {
                db.FeeConcessions.Add(new FeeConcession
                {
                    StudentId = cohort[i].Id,
                    AcademicYearId = year.Id,
                    FeeHeadId = tuition.Id,
                    Amount = concessionsAdded == 0 ? 2_000m : 5_000m,
                    Reason = concessionsAdded == 0 ? "Sibling discount" : "Merit scholarship",
                });
                concessionsAdded++;
            }
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static void AddPayment(
        AppDbContext db, Guid yearId, Guid studentId, decimal amount, DateOnly paidOn, ref int receipt)
    {
        db.FeePayments.Add(new FeePayment
        {
            StudentId = studentId,
            AcademicYearId = yearId,
            ReceiptNumber = $"RCP-2026-{receipt++}",
            Amount = amount,
            PaidOn = paidOn,
            Mode = receipt % 2 == 0 ? PaymentMode.Cash : PaymentMode.Upi,
        });
    }

    // --- timetable ---------------------------------------------------------

    private static async Task EnsureTimetableAsync(
        AppDbContext db, Guid grade5Id, Guid grade6Id,
        Dictionary<string, Subject> subjects, Dictionary<string, Teacher> teachers)
    {
        // Who teaches what, per class (drives the teaching-outcomes insights).
        var plan = new Dictionary<(Guid ClassId, string Subject), Teacher>
        {
            [(grade5Id, "Mathematics")] = teachers["Anita Rao"],
            [(grade5Id, "Science")] = teachers["Ravi Prasad"],
            [(grade5Id, "English")] = teachers["Sunita Devi"],
            [(grade5Id, "Telugu")] = teachers["Ravi Prasad"],
            [(grade5Id, "Social Studies")] = teachers["Anita Rao"],
            [(grade6Id, "Mathematics")] = teachers["Vikram Sharma"],
            [(grade6Id, "Science")] = teachers["Vikram Sharma"],
            [(grade6Id, "English")] = teachers["Sunita Devi"],
            [(grade6Id, "Telugu")] = teachers["Ravi Prasad"],
            [(grade6Id, "Social Studies")] = teachers["Anita Rao"],
        };
        var rotation = new[] { "Mathematics", "English", "Science", "Telugu", "Social Studies" };

        // Tracked, not projected: demo databases seeded before breaks existed
        // have their periods running back to back, which now overlaps the
        // recess being added below. Those rows are corrected in place rather
        // than left contradicting the break they sit under.
        var lessons = await db.TimetableEntries
            .Where(e => e.SlotKind == TimetableSlotKind.Lesson && e.SectionId == null &&
                        (e.SchoolClassId == grade5Id || e.SchoolClassId == grade6Id))
            .ToListAsync()
            .ConfigureAwait(false);
        var existing = lessons
            .Select(e => (e.SchoolClassId, e.DayOfWeek, e.Period))
            .ToHashSet();
        // Grouped rather than keyed one-to-one: nothing in the schema stops a
        // slot having two live rows, and this runs at every dev startup — a
        // duplicate key here would take the whole API down on boot.
        var bySlot = lessons
            .GroupBy(e => (e.SchoolClassId, e.DayOfWeek, e.Period))
            .ToDictionary(g => g.Key, g => g.ToList());

        var existingBreaks = (await db.TimetableEntries
                .Where(e => e.SlotKind != TimetableSlotKind.Lesson)
                .Select(e => new { e.SchoolClassId, e.DayOfWeek, e.SlotKind })
                .ToListAsync()
                .ConfigureAwait(false))
            .Select(e => (e.SchoolClassId, e.DayOfWeek, e.SlotKind))
            .ToHashSet();

        // A real Indian school day: two periods, recess, two more, then lunch.
        // Periods from the third onwards start 20 minutes later to make room
        // for the recess rather than overlapping it.
        var added = 0;
        foreach (var classId in new[] { grade5Id, grade6Id })
        {
            for (var day = 1; day <= 6; day++) // Mon–Sat
            {
                var periods = day == 6 ? 2 : 4; // Saturday is a short day
                for (var period = 1; period <= periods; period++)
                {
                    var shift = (period - 1) * 45 + (period >= 3 ? 20 : 0);
                    var start = new TimeOnly(8, 0).AddMinutes(shift);
                    var end = new TimeOnly(8, 45).AddMinutes(shift);

                    if (existing.Contains((classId, day, (int?)period)))
                    {
                        foreach (var current in bySlot[(classId, day, (int?)period)]
                                     .Where(e => e.StartTime != start))
                        {
                            current.StartTime = start;
                            current.EndTime = end;
                            added++;
                        }

                        continue;
                    }

                    var subjectName = rotation[(day + period + (classId == grade6Id ? 2 : 0)) % rotation.Length];
                    var teacher = plan[(classId, subjectName)];
                    db.TimetableEntries.Add(new TimetableEntry
                    {
                        SchoolClassId = classId,
                        SectionId = null, // class-wide, like the hand-made demo slots
                        DayOfWeek = day,
                        Period = period,
                        StartTime = start,
                        EndTime = end,
                        SubjectId = subjects[subjectName].Id,
                        TeacherId = teacher.Id,
                        TeacherName = teacher.FullName,
                        IsPublished = true,
                    });
                    added++;
                }

                if (periods < 4)
                {
                    continue; // no breaks on the short Saturday
                }

                // Deliberately unnamed: a label is school-entered text and
                // would show verbatim, so leaving it off lets each app fall
                // back to its own translation — a Telugu parent reads
                // "విరామం", not "Recess". Schools that call it something
                // particular ("Tiffin break") type that in and it wins.
                added += AddBreak(
                    db, existingBreaks, classId, day, TimetableSlotKind.Break,
                    new TimeOnly(9, 30), new TimeOnly(9, 50));
                added += AddBreak(
                    db, existingBreaks, classId, day, TimetableSlotKind.Lunch,
                    new TimeOnly(11, 20), new TimeOnly(12, 0));
            }
        }

        if (added > 0)
        {
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Adds one break slot unless that class already has that kind on that day.</summary>
    private static int AddBreak(
        AppDbContext db,
        HashSet<(Guid ClassId, int Day, TimetableSlotKind Kind)> existing,
        Guid classId,
        int day,
        TimetableSlotKind kind,
        TimeOnly start,
        TimeOnly end)
    {
        if (!existing.Add((classId, day, kind)))
        {
            return 0;
        }

        db.TimetableEntries.Add(new TimetableEntry
        {
            SchoolClassId = classId,
            SectionId = null,
            DayOfWeek = day,
            SlotKind = kind,
            Period = null,
            StartTime = start,
            EndTime = end,
            Label = null,
            IsPublished = true,
        });
        return 1;
    }

    private static async Task EnsureSubstitutionsAsync(
        AppDbContext db, Dictionary<string, Teacher> teachers, DateOnly today)
    {
        if (await db.TimetableSubstitutions.CountAsync().ConfigureAwait(false) >= 5)
        {
            return;
        }

        // Cover a real slot of each absent teacher on a past weekday this month.
        var covers = new[]
        {
            (Absent: teachers["Anita Rao"], By: teachers["Sunita Devi"]),
            (Absent: teachers["Vikram Sharma"], By: teachers["Ravi Prasad"]),
            (Absent: teachers["Sunita Devi"], By: teachers["Anita Rao"]),
        };
        // Recent non-Sunday dates, newest first, so the covers land this month.
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var dates = new List<DateOnly>();
        for (var d = today; d >= monthStart && dates.Count < covers.Length; d = d.AddDays(-1))
        {
            if (d.DayOfWeek != DayOfWeek.Sunday)
            {
                dates.Add(d);
            }
        }

        for (var i = 0; i < covers.Length && i < dates.Count; i++)
        {
            var cover = covers[i];
            var date = dates[i];
            var slot = await db.TimetableEntries
                .Where(e => e.TeacherId == cover.Absent.Id && e.IsPublished)
                .OrderBy(e => e.DayOfWeek).ThenBy(e => e.Period)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (slot is null)
            {
                continue;
            }

            var exists = await db.TimetableSubstitutions.AnyAsync(s =>
                    s.TimetableEntryId == slot.Id && s.Date == date)
                .ConfigureAwait(false);
            if (!exists)
            {
                db.TimetableSubstitutions.Add(new TimetableSubstitution
                {
                    Date = date,
                    TimetableEntryId = slot.Id,
                    AbsentTeacherId = cover.Absent.Id,
                    SubstituteTeacherId = cover.By.Id,
                });
            }
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    // --- admissions pipeline ----------------------------------------------

    private static async Task EnsureEnquiriesAsync(AppDbContext db, DateOnly today)
    {
        if (await db.AdmissionEnquiries.CountAsync().ConfigureAwait(false) >= 6)
        {
            return;
        }

        var rows = new (string Child, string Parent, string Phone, string Class,
            EnquirySource Source, EnquiryStatus Status, DateOnly? FollowUp, string? Notes)[]
        {
            ("Aadhya Verma", "Rohit Verma", "+919876600001", "Grade 5",
                EnquirySource.Website, EnquiryStatus.New, today.AddDays(-2), "Filled the website form; asked for a fee card"),
            ("Vihaan Gupta", "Neha Gupta", "+919876600002", "Grade 6",
                EnquirySource.Phone, EnquiryStatus.New, today.AddDays(1), "Wants CBSE affiliation details"),
            ("Sara Khan", "Imran Khan", "+919876600003", "Grade 5",
                EnquirySource.WalkIn, EnquiryStatus.Contacted, today, "Visited reception; shared brochure"),
            ("Reyansh Iyer", "Divya Iyer", "+919876600004", "Grade 6",
                EnquirySource.Referral, EnquiryStatus.Contacted, today.AddDays(3), "Referred by the Reddy family"),
            ("Myra Joshi", "Kunal Joshi", "+919876600005", "Grade 5",
                EnquirySource.Website, EnquiryStatus.Visit, today.AddDays(2), "Campus tour booked for the weekend"),
            ("Arnav Rao", "Sneha Rao", "+919876600006", "Grade 6",
                EnquirySource.Phone, EnquiryStatus.Lost, null, "Chose a school closer to home"),
            ("Anvi Sharma", "Manish Sharma", "+919876600007", "Grade 5",
                EnquirySource.WalkIn, EnquiryStatus.Lost, null, "Fees beyond budget this year"),
        };
        foreach (var row in rows)
        {
            if (await db.AdmissionEnquiries.AnyAsync(e => e.Phone == row.Phone).ConfigureAwait(false))
            {
                continue;
            }

            db.AdmissionEnquiries.Add(new AdmissionEnquiry
            {
                ChildName = row.Child,
                ParentName = row.Parent,
                Phone = row.Phone,
                AppliedClass = row.Class,
                Source = row.Source,
                Status = row.Status,
                FollowUpOn = row.FollowUp,
                Notes = row.Notes,
            });
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    // --- notices, homework -------------------------------------------------

    private static async Task EnsureCommunicationAsync(
        AppDbContext db, Guid grade5Id, Dictionary<string, Subject> subjects, DateOnly today)
    {
        if (await db.Notices.CountAsync().ConfigureAwait(false) < 4)
        {
            var notices = new (string Title, string Body, bool Pinned)[]
            {
                ("Parent-teacher meeting", "PTM for all classes on 20 August, 9 AM – 12 noon. Report cards will be shared.", true),
                ("Annual sports day", "Sports day on 5 September at the school grounds. Practice starts next week.", false),
                ("Fee reminder — Term 1", "Term 1 tuition is due by the first week of September. Pay online from the parent app.", false),
            };
            foreach (var notice in notices)
            {
                if (!await db.Notices.AnyAsync(n => n.Title == notice.Title).ConfigureAwait(false))
                {
                    db.Notices.Add(new Notice
                    {
                        Title = notice.Title,
                        Body = notice.Body,
                        IsPinned = notice.Pinned,
                        ExpiresOn = today.AddDays(45),
                    });
                }
            }
        }

        if (await db.HomeworkAssignments.CountAsync().ConfigureAwait(false) < 4)
        {
            var rows = new (string Subject, string Title, string Instructions)[]
            {
                ("Science", "Leaf collection", "Collect five different leaves and label their trees."),
                ("English", "Book review", "Write ten lines about the last story you read."),
                ("Telugu", "Handwriting practice", "Copy the poem on page 12 in your best handwriting."),
            };
            foreach (var row in rows)
            {
                if (!await db.HomeworkAssignments.AnyAsync(h => h.Title == row.Title).ConfigureAwait(false))
                {
                    db.HomeworkAssignments.Add(new HomeworkAssignment
                    {
                        SchoolClassId = grade5Id,
                        SubjectId = subjects[row.Subject].Id,
                        Title = row.Title,
                        Instructions = row.Instructions,
                        AssignedOn = today,
                        DueDate = today.AddDays(3),
                    });
                }
            }
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    // --- library -----------------------------------------------------------

    private static async Task EnsureLibraryAsync(AppDbContext db, DateOnly today)
    {
        if (await db.Books.CountAsync().ConfigureAwait(false) >= 5)
        {
            return;
        }

        var books = new (string Title, string Author, string Category)[]
        {
            ("The Jungle Book", "Rudyard Kipling", "Fiction"),
            ("Malgudi Days", "R.K. Narayan", "Fiction"),
            ("A Brief History of Time", "Stephen Hawking", "Science"),
            ("Panchatantra Tales", "Vishnu Sharma", "Folk tales"),
            ("Charlotte's Web", "E.B. White", "Fiction"),
        };
        var created = new List<Book>();
        foreach (var (title, author, category) in books)
        {
            if (!await db.Books.AnyAsync(b => b.Title == title).ConfigureAwait(false))
            {
                var book = new Book
                {
                    Title = title,
                    Author = author,
                    Category = category,
                    CopiesTotal = 3,
                    CopiesAvailable = 3,
                };
                db.Books.Add(book);
                created.Add(book);
            }
        }

        // A few live loans, one overdue, drawn from the seeded cohort.
        var borrowers = await db.Students
            .Where(s => s.AdmissionNumber.StartsWith("ADM-2026-01"))
            .OrderBy(s => s.AdmissionNumber)
            .Take(3)
            .ToListAsync()
            .ConfigureAwait(false);
        for (var i = 0; i < Math.Min(created.Count, borrowers.Count); i++)
        {
            created[i].CopiesAvailable--;
            db.BookLoans.Add(new BookLoan
            {
                BookId = created[i].Id,
                StudentId = borrowers[i].Id,
                IssuedOn = today.AddDays(-10 - i * 3),
                // The first loan is overdue so the dashboard tile lights up.
                DueOn = i == 0 ? today.AddDays(-2) : today.AddDays(4 + i * 3),
            });
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    // --- leave -------------------------------------------------------------

    private static async Task EnsureLeaveAsync(AppDbContext db, DateOnly today)
    {
        if (await db.LeaveRequests.AnyAsync(l => l.Status == LeaveRequestStatus.Pending)
                .ConfigureAwait(false))
        {
            return;
        }

        var student = await db.Students
            .Where(s => s.AdmissionNumber.StartsWith("ADM-2026-01"))
            .OrderBy(s => s.AdmissionNumber)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        var requester = await db.Users
            .Where(u => u.Email == "parent@demo.school")
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (student is null || requester is null)
        {
            return;
        }

        db.LeaveRequests.Add(new LeaveRequest
        {
            Kind = LeaveApplicantKind.Student,
            StudentId = student.Id,
            RequestedByUserId = requester.Value,
            FromDate = today.AddDays(4),
            ToDate = today.AddDays(5),
            Reason = "Cousin's wedding in Warangal",
        });
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    // --- transport ---------------------------------------------------------

    private static async Task EnsureTransportAsync(AppDbContext db)
    {
        var stops = await db.RouteStops
            .OrderBy(s => s.SortOrder)
            .Take(2)
            .ToListAsync()
            .ConfigureAwait(false);
        if (stops.Count == 0)
        {
            return; // no routes configured in this environment
        }

        var assigned = await db.StudentTransportAssignments
            .Select(a => a.StudentId)
            .ToListAsync()
            .ConfigureAwait(false);
        var riders = await db.Students
            .Where(s => s.AdmissionNumber.StartsWith("ADM-2026-01") && !assigned.Contains(s.Id))
            .OrderBy(s => s.AdmissionNumber)
            .Take(4)
            .ToListAsync()
            .ConfigureAwait(false);

        for (var i = 0; i < riders.Count; i++)
        {
            var stop = stops[i % stops.Count];
            db.StudentTransportAssignments.Add(new StudentTransportAssignment
            {
                StudentId = riders[i].Id,
                RouteId = stop.RouteId,
                StopId = stop.Id,
            });
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Demo school enriched with {Students} seeded students plus attendance, marks, fees, timetable and admissions data")]
    private static partial void LogSeeded(ILogger logger, int students);
}
