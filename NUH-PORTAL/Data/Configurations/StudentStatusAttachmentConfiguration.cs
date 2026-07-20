using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class StudentStatusAttachmentConfiguration : IEntityTypeConfiguration<StudentStatusAttachment>
    {
        public void Configure(EntityTypeBuilder<StudentStatusAttachment> builder)
        {
            builder.ToTable("StudentStatusAttachments");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.StudentStatusActionId).HasColumnName("student_status_action_id");
            builder.Property(a => a.FileName).HasColumnName("file_name");
            builder.Property(a => a.OriginalFileName).HasColumnName("original_file_name");
            builder.Property(a => a.ContentType).HasColumnName("content_type");
            builder.Property(a => a.FileSize).HasColumnName("file_size");
            builder.Property(a => a.UploadedBy).HasColumnName("uploaded_by");
            builder.Property(a => a.UploadedAt).HasColumnName("uploaded_at")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            builder.HasOne(a => a.StudentStatusAction)
                .WithMany()
                .HasForeignKey(a => a.StudentStatusActionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.UploadedByUser)
                .WithMany()
                .HasForeignKey(a => a.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
