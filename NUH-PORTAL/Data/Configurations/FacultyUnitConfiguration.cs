using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Data.Converters;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class FacultyUnitConfiguration : IEntityTypeConfiguration<FacultyUnit>
    {
        public void Configure(EntityTypeBuilder<FacultyUnit> builder)
        {
            builder.ToTable("FacultyUnits");
            builder.HasKey(u => u.Id);

            builder.Property(u => u.UnitType).HasConversion(new FacultyUnitTypeConverter()).HasMaxLength(20).IsRequired();
            builder.Property(u => u.Status).HasConversion(new FacultyUnitStatusConverter()).HasMaxLength(20).IsRequired();
            builder.Property(u => u.SyncState).HasConversion(new FacultyUnitSyncStateConverter()).HasMaxLength(20).IsRequired();

            builder.Property(u => u.AdAccount).HasMaxLength(64).IsRequired();
            builder.Property(u => u.AdDistinguishedName).HasMaxLength(512);
            builder.Property(u => u.AdUserPrincipalName).HasMaxLength(256);
            builder.Property(u => u.LastSyncError).HasMaxLength(1000);
            builder.Property(u => u.Notes).HasMaxLength(1000);

            // ⚠️ اسم الحساب مفتاح الربط بالدومين — لازم يكون فريد. لو اتكرر
            //    بقى عندنا وحدتين بتكتبوا فوق بعض في نفس حساب الدومين، وآخر
            //    واحد بيحفظ بيمسح اللي قبله من غير ما حد ياخد باله.
            builder.HasIndex(u => u.AdAccount).IsUnique().HasDatabaseName("UX_FacultyUnits_ad_account");
            builder.HasIndex(u => new { u.UnitType, u.TowerNo, u.ApartmentNo }).HasDatabaseName("IX_FacultyUnits_tower");
            builder.HasIndex(u => new { u.UnitType, u.VillaNo }).HasDatabaseName("IX_FacultyUnits_villa");
            builder.HasIndex(u => u.Status).HasDatabaseName("IX_FacultyUnits_status");

            // ⚠️ NoAction مش Cascade: مسح موظف من النظام مايمسحش تاريخ الوحدات
            //    اللي هو أنشأها. الاسم بيفضل منسوب لإجراءاته زي سجل العمليات.
            builder.HasOne(u => u.CreatedByUser).WithMany()
                   .HasForeignKey(u => u.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(u => u.UpdatedByUser).WithMany()
                   .HasForeignKey(u => u.UpdatedBy).OnDelete(DeleteBehavior.NoAction);

            builder.Ignore(u => u.DisplayNameAr);
            // محسوبة من AdUserPrincipalName و AdAccount - لا عمود لها
            builder.Ignore(u => u.UpnMatchesAccount);
        }
    }
}
