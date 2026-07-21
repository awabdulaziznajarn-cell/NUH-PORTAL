using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class StudentConfiguration : IEntityTypeConfiguration<Student>
    {
        public void Configure(EntityTypeBuilder<Student> builder)
        {
            builder.ToTable("Students");
            builder.HasKey(s => s.Id);

            builder.HasIndex(s => s.student_id).IsUnique();
            builder.HasIndex(s => s.national_id).IsUnique();

            // البحث بالجوال (تتبع/OTP) والعدّادات (محذوف + حالة)
            builder.HasIndex(s => s.phone)
                .HasDatabaseName("IX_Students_phone");

            builder.HasIndex(s => new { s.IsDeleted, s.status })
                .HasDatabaseName("IX_Students_deleted_status");
        }
    }
}
