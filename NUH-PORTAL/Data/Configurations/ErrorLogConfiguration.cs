using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class ErrorLogConfiguration : IEntityTypeConfiguration<ErrorLog>
    {
        public void Configure(EntityTypeBuilder<ErrorLog> builder)
        {
            builder.ToTable("ErrorLogs");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.occurred_at)
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            builder.HasIndex(e => e.occurred_at).HasDatabaseName("IX_ErrorLogs_occurred_at");

            // الصفحة بتفلتر بالمسار والنوع
            builder.Property(e => e.request_path).HasMaxLength(512);
            builder.Property(e => e.request_method).HasMaxLength(16);
            builder.Property(e => e.exception_type).HasMaxLength(256);
            builder.Property(e => e.username).HasMaxLength(256);

            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.user_id)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
