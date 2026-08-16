using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Data.Converters;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            // نخلّي جدول Identity اسمه Users (بدل AspNetUsers) — أقل اختلاف عن السكيمة الحالية.
            // تفرّد اسم المستخدم بيتولّى من Identity (فهرس على NormalizedUserName) فمش محتاجينه هنا.
            builder.ToTable("Users");

            // الجوال فريد لما يكون موجود (مسار OTP بيدور بيه وبينشئ مستخدم لو مش لاقيه)
            builder.HasIndex(u => u.mobile)
                .IsUnique()
                .HasDatabaseName("IX_Users_mobile")
                .HasFilter("[mobile] IS NOT NULL");

            builder.Property(u => u.auth_source).HasMaxLength(16);

            // نفس المحوّل بتاع جنس الطالب — القيم في القاعدة "male"/"female"
            builder.Property(u => u.scope_gender).HasConversion(new GenderConverter()).HasMaxLength(10);

            // كل قراءات شاشة المستخدمين بتستثني المحذوفين، فالفهرس ده بيخدمها كلها
            builder.HasIndex(u => u.is_deleted).HasDatabaseName("IX_Users_is_deleted");
        }
    }
}
