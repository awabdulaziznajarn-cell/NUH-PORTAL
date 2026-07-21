using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.action_at)
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            // صفحة السجل بترتب بالتاريخ وبتفلتر بالإجراء
            builder.HasIndex(a => a.action_at)
                .HasDatabaseName("IX_AuditLogs_action_at");

            builder.HasIndex(a => a.action)
                .HasDatabaseName("IX_AuditLogs_action");

            builder.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.user_id)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
