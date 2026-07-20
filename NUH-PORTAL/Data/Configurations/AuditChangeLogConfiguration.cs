using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class AuditChangeLogConfiguration : IEntityTypeConfiguration<AuditChangeLog>
    {
        public void Configure(EntityTypeBuilder<AuditChangeLog> builder)
        {
            builder.ToTable("AuditChangeLogs");
            builder.HasKey(a => a.Id);

            builder.HasOne(a => a.AuditLog)
                .WithMany(al => al.AuditChangeLogs)
                .HasForeignKey(a => a.AuditLogId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
