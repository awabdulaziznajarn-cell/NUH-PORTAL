using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");
            builder.HasKey(u => u.Id);

            // اسم المستخدم لازم يكون فريد — اللوجين بيدور بيه
            builder.HasIndex(u => u.username)
                .IsUnique()
                .HasDatabaseName("IX_Users_username")
                .HasFilter("[username] IS NOT NULL");

            // الجوال فريد لما يكون موجود (مسار OTP بيدور بيه وبينشئ مستخدم لو مش لاقيه)
            builder.HasIndex(u => u.mobile)
                .IsUnique()
                .HasDatabaseName("IX_Users_mobile")
                .HasFilter("[mobile] IS NOT NULL");
        }
    }
}
