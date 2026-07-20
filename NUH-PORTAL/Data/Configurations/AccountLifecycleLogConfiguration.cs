using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class AccountLifecycleLogConfiguration : IEntityTypeConfiguration<AccountLifecycleLog>
    {
        public void Configure(EntityTypeBuilder<AccountLifecycleLog> builder)
        {
            builder.ToTable("AccountLifecycleLogs");
            builder.HasKey(l => l.Id);

            builder.Property(l => l.StudentId).HasColumnName("student_id");
            builder.Property(l => l.Action).HasColumnName("action");
            builder.Property(l => l.PerformedBy).HasColumnName("performed_by");
            builder.Property(l => l.PerformedAt).HasColumnName("performed_at")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            builder.Property(l => l.Details).HasColumnName("details");
            builder.Property(l => l.IpAddress).HasColumnName("ip_address");

            builder.HasOne(l => l.Student)
                .WithMany()
                .HasForeignKey(l => l.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(l => l.Performer)
                .WithMany()
                .HasForeignKey(l => l.PerformedBy)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
