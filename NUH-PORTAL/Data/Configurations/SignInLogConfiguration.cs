using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class SignInLogConfiguration : IEntityTypeConfiguration<SignInLog>
    {
        public void Configure(EntityTypeBuilder<SignInLog> builder)
        {
            builder.ToTable("SignInLogs");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.occurred_at)
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            builder.Property(s => s.event_type).HasMaxLength(32);
            builder.Property(s => s.method).HasMaxLength(32);
            builder.Property(s => s.username).HasMaxLength(256);

            builder.HasIndex(s => s.occurred_at).HasDatabaseName("IX_SignInLogs_occurred_at");
            builder.HasIndex(s => s.event_type).HasDatabaseName("IX_SignInLogs_event_type");

            builder.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.user_id)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
