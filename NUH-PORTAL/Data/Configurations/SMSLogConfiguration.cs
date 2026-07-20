using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class SMSLogConfiguration : IEntityTypeConfiguration<SMSLog>
    {
        public void Configure(EntityTypeBuilder<SMSLog> builder)
        {
            builder.ToTable("SMSLogs");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Mobile).HasColumnName("mobile");
            builder.Property(s => s.Provider).HasColumnName("provider");
            builder.Property(s => s.Message).HasColumnName("message");
            builder.Property(s => s.Status).HasColumnName("status");
            builder.Property(s => s.SentDate).HasColumnName("sent_date")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            builder.HasIndex(s => s.Status)
                .HasDatabaseName("IX_SMSLogs_status");
        }
    }
}
