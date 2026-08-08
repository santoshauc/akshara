using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Hostel;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for hostel buildings.</summary>
public sealed class HostelBuildingConfiguration : IEntityTypeConfiguration<HostelBuilding>
{
    public void Configure(EntityTypeBuilder<HostelBuilding> builder)
    {
        builder.ToTable("hostels");
        builder.Property(h => h.Name).HasMaxLength(128).IsRequired();
        builder.Property(h => h.WardenName).HasMaxLength(128);
        builder.Property(h => h.WardenPhone).HasMaxLength(20);
        builder.HasIndex(h => new { h.TenantId, h.Name }).IsUnique();
    }
}

/// <summary>Mapping for hostel rooms.</summary>
public sealed class HostelRoomConfiguration : IEntityTypeConfiguration<HostelRoom>
{
    public void Configure(EntityTypeBuilder<HostelRoom> builder)
    {
        builder.ToTable("hostel_rooms");
        builder.Property(r => r.RoomNumber).HasMaxLength(16).IsRequired();
        builder.HasIndex(r => new { r.TenantId, r.HostelId, r.RoomNumber }).IsUnique();

        builder.HasOne(r => r.Hostel).WithMany()
            .HasForeignKey(r => r.HostelId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for hostel allocations.</summary>
public sealed class HostelAllocationConfiguration : IEntityTypeConfiguration<HostelAllocation>
{
    public void Configure(EntityTypeBuilder<HostelAllocation> builder)
    {
        builder.ToTable("hostel_allocations");

        builder.HasOne(a => a.Room).WithMany()
            .HasForeignKey(a => a.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Student).WithMany()
            .HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TenantId, a.StudentId, a.VacatedOn });
        builder.HasIndex(a => new { a.TenantId, a.RoomId, a.VacatedOn });
    }
}
