using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Data.Converters;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class StudentConfiguration : IEntityTypeConfiguration<Student>
    {
        public void Configure(EntityTypeBuilder<Student> builder)
        {
            builder.ToTable("Students");
            builder.HasKey(s => s.Id);

            // gender: enum متخزّن كـ "male"/"female" (نفس القيم الحالية — بدون تغيير عمود ولا ترحيل)
            builder.Property(s => s.gender).HasConversion(new GenderConverter()).HasMaxLength(20);
            // ad_status: enum متخزّن كـ "enabled"/"disabled" (نفس القيم — بدون تغيير عمود ولا ترحيل)
            builder.Property(s => s.ad_status).HasConversion(new AdStatusConverter()).HasMaxLength(20);
            // student_status: enum متخزّن كنص زي ما هو (active/dismissed/graduated/transferred/left_housing)
            builder.Property(s => s.student_status).HasConversion(new StudentStatusConverter()).HasMaxLength(30);
            // status: enum متخزّن كنص (active/inactive/left)
            builder.Property(s => s.status).HasConversion(new StudentStateConverter()).HasMaxLength(20);

            // الدور: كود قصير ("0" للأرضي، "1".."4") — nvarchar(20) بدل nvarchar(max)
            builder.Property(s => s.floor_number).HasMaxLength(20);

            builder.HasIndex(s => s.student_id).IsUnique();
            builder.HasIndex(s => s.national_id).IsUnique();

            // البحث بالجوال (تتبع/OTP) والعدّادات (محذوف + حالة)
            builder.HasIndex(s => s.phone)
                .HasDatabaseName("IX_Students_phone");

            builder.HasIndex(s => new { s.IsDeleted, s.status })
                .HasDatabaseName("IX_Students_deleted_status");

            // العلاقات بالقوائم المرجعية — Restrict عشان مايتحذفش عنصر lookup مرتبط بطلاب
            builder.HasOne(s => s.CollegeRef)
                .WithMany()
                .HasForeignKey(s => s.CollegeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.DepartmentRef)
                .WithMany()
                .HasForeignKey(s => s.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.BuildingRef)
                .WithMany()
                .HasForeignKey(s => s.BuildingId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.AcademicLevelRef)
                .WithMany()
                .HasForeignKey(s => s.AcademicLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
