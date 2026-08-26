using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Data.Converters;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class FacultyOccupancyConfiguration : IEntityTypeConfiguration<FacultyOccupancy>
    {
        public void Configure(EntityTypeBuilder<FacultyOccupancy> builder)
        {
            builder.ToTable("FacultyOccupancies");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.FullNameAr).HasMaxLength(250).IsRequired();
            builder.Property(o => o.FullNameEn).HasMaxLength(250);
            builder.Property(o => o.NationalId).HasMaxLength(20);
            builder.Property(o => o.Mobile).HasMaxLength(20);
            builder.Property(o => o.College).HasMaxLength(200);
            builder.Property(o => o.Department).HasMaxLength(200);
            builder.Property(o => o.TicketNo).HasMaxLength(50);
            builder.Property(o => o.EndReasonNote).HasMaxLength(500);
            builder.Property(o => o.AttachmentPath).HasMaxLength(500);
            builder.Property(o => o.OriginalFileName).HasMaxLength(260);
            builder.Property(o => o.EndReason).HasConversion(new OccupancyEndReasonConverter()).HasMaxLength(30);
            builder.Property(o => o.Gender).HasConversion(new GenderConverter()).HasMaxLength(20);

            builder.HasIndex(o => o.UnitId).HasDatabaseName("IX_FacultyOccupancies_unit");
            builder.HasIndex(o => o.NationalId).HasDatabaseName("IX_FacultyOccupancies_national_id");
            builder.HasIndex(o => o.TicketNo).HasDatabaseName("IX_FacultyOccupancies_ticket");

            // ⚠️ الفهرس اللي بيضمن ساكن واحد مفتوح لكل وحدة اتعمل في السكربت
            //    AddFacultyHousing.sql مباشرة، مش هنا. السبب إن EF Core بيولّد
            //    الفلترة كـ "[EndDate] IS NULL" وهي مظبوطة، لكن أي migration
            //    جاي بيقارن الشكل النصّي وبيفضل يحاول يعيد إنشاء الفهرس كل مرة.
            //    مكتوب هناك مرة واحدة وواضح.

            builder.HasOne(o => o.Unit).WithMany(u => u.Occupancies)
                   .HasForeignKey(o => o.UnitId).OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(o => o.CreatedByUser).WithMany()
                   .HasForeignKey(o => o.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(o => o.ClosedByUser).WithMany()
                   .HasForeignKey(o => o.ClosedBy).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(o => o.ConfirmedByUser).WithMany()
                   .HasForeignKey(o => o.ConfirmedBy).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
