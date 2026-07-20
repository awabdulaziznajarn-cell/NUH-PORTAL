using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class HousingTransferConfiguration : IEntityTypeConfiguration<HousingTransfer>
    {
        public void Configure(EntityTypeBuilder<HousingTransfer> builder)
        {
            builder.ToTable("HousingTransfers");
            builder.HasKey(h => h.Id);

            builder.Property(h => h.StudentId).HasColumnName("student_id");
            builder.Property(h => h.StudentNumber).HasColumnName("student_number");
            builder.Property(h => h.OldBuilding).HasColumnName("old_building");
            builder.Property(h => h.OldApartment).HasColumnName("old_apartment");
            builder.Property(h => h.OldRoom).HasColumnName("old_room");
            builder.Property(h => h.NewBuilding).HasColumnName("new_building");
            builder.Property(h => h.NewApartment).HasColumnName("new_apartment");
            builder.Property(h => h.NewRoom).HasColumnName("new_room");
            builder.Property(h => h.Reason).HasColumnName("reason");
            builder.Property(h => h.CustomReason).HasColumnName("custom_reason");
            builder.Property(h => h.AttachmentPath).HasColumnName("attachment_path");
            builder.Property(h => h.OriginalFileName).HasColumnName("original_file_name");
            builder.Property(h => h.CreatedBy).HasColumnName("created_by");
            builder.Property(h => h.CreatedAt).HasColumnName("created_at")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            builder.HasOne(h => h.Student)
                .WithMany()
                .HasForeignKey(h => h.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(h => h.CreatedByUser)
                .WithMany()
                .HasForeignKey(h => h.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
