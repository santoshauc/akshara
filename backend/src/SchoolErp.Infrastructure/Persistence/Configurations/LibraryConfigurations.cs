using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Library;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for library books.</summary>
public sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");
        builder.Property(b => b.Title).HasMaxLength(256).IsRequired();
        builder.Property(b => b.Author).HasMaxLength(128).IsRequired();
        builder.Property(b => b.Isbn).HasMaxLength(20);
        builder.Property(b => b.Category).HasMaxLength(64);
        builder.HasIndex(b => new { b.TenantId, b.Title });
    }
}

/// <summary>Mapping for book loans.</summary>
public sealed class BookLoanConfiguration : IEntityTypeConfiguration<BookLoan>
{
    public void Configure(EntityTypeBuilder<BookLoan> builder)
    {
        builder.ToTable("book_loans");

        builder.HasOne(l => l.Book).WithMany()
            .HasForeignKey(l => l.BookId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.Student).WithMany()
            .HasForeignKey(l => l.StudentId).OnDelete(DeleteBehavior.Restrict);

        // Open-loan lookups by student and by book drive every screen.
        builder.HasIndex(l => new { l.TenantId, l.StudentId, l.ReturnedOn });
        builder.HasIndex(l => new { l.TenantId, l.BookId, l.ReturnedOn });
    }
}
