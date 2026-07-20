using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class OTPVerificationConfiguration : IEntityTypeConfiguration<OTPVerification>
    {
        public void Configure(EntityTypeBuilder<OTPVerification> builder)
        {
            builder.ToTable("OTPVerifications");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.Mobile).HasColumnName("mobile");
            builder.Property(o => o.OTPHash).HasColumnName("otp_hash");
            builder.Property(o => o.CreatedAt).HasColumnName("created_at")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            builder.Property(o => o.ExpiresAt).HasColumnName("expires_at")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            builder.Property(o => o.VerifiedAt).HasColumnName("verified_at")
                .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
            builder.Property(o => o.Attempts).HasColumnName("attempts");
            builder.Property(o => o.IPAddress).HasColumnName("ip_address");

            builder.HasIndex(o => new { o.Mobile, o.VerifiedAt })
                .HasDatabaseName("IX_OTPVerifications_mobile_verified");

            builder.HasIndex(o => o.ExpiresAt)
                .HasDatabaseName("IX_OTPVerifications_expires")
                .HasFilter("[verified_at] IS NULL");
        }
    }
}
