using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolErp.Domain.Auth;

namespace SchoolErp.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for refresh tokens.</summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
        builder.Property(t => t.CreatedByIp).HasMaxLength(45);
        builder.Property(t => t.RevokedByIp).HasMaxLength(45);
        builder.Property(t => t.RevocationReason).HasMaxLength(32);
        builder.Property(t => t.DeviceName).HasMaxLength(128);

        // Tokens are only ever looked up by exact hash.
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
    }
}

/// <summary>Mapping for OTP codes.</summary>
public sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("otp_codes");

        builder.Property(o => o.Phone).HasMaxLength(20).IsRequired();
        builder.Property(o => o.CodeHash).HasMaxLength(64).IsRequired();

        builder.HasIndex(o => new { o.TenantId, o.Phone });
    }
}
