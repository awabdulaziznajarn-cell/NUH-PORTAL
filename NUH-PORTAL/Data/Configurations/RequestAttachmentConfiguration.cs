using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class RequestAttachmentConfiguration : IEntityTypeConfiguration<RequestAttachment>
    {
        public void Configure(EntityTypeBuilder<RequestAttachment> builder)
        {
            builder.ToTable("RequestAttachments");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.RequestId).HasColumnName("request_id");
            builder.Property(a => a.FileName).HasColumnName("file_name");
            builder.Property(a => a.OriginalFileName).HasColumnName("original_file_name");
            builder.Property(a => a.ContentType).HasColumnName("content_type");
            builder.Property(a => a.FileSize).HasColumnName("file_size");
            builder.Property(a => a.DocumentType).HasColumnName("document_type");
            builder.Property(a => a.Notes).HasColumnName("notes");
            builder.Property(a => a.UploadedBy).HasColumnName("uploaded_by");
            builder.Property(a => a.UploadedAt).HasColumnName("uploaded_at")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            builder.Property(a => a.IsDeleted).HasColumnName("is_deleted");

            builder.HasIndex(a => new { a.RequestId, a.IsDeleted })
                .HasDatabaseName("IX_RequestAttachments_request_deleted");

            builder.HasOne(a => a.Request)
                .WithMany()
                .HasForeignKey(a => a.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.UploadedByUser)
                .WithMany()
                .HasForeignKey(a => a.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
