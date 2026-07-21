using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class RequestConfiguration : IEntityTypeConfiguration<Request>
    {
        public void Configure(EntityTypeBuilder<Request> builder)
        {
            builder.ToTable("Requests");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.RequestType).HasColumnName("request_type");
            builder.Property(r => r.StudentId).HasColumnName("student_id");
            builder.Property(r => r.SubmittedBy).HasColumnName("submitted_by");
            builder.Property(r => r.Status).HasColumnName("status");
            builder.Property(r => r.Notes).HasColumnName("notes");
            builder.Property(r => r.SubmittedAt).HasColumnName("submitted_at");
            builder.Property(r => r.ReviewedAt).HasColumnName("reviewed_at");
            builder.Property(r => r.ReviewedBy).HasColumnName("reviewed_by");
            builder.Property(r => r.RequestedByRole).HasColumnName("requested_by_role");
            builder.Property(r => r.HousingReviewedBy).HasColumnName("housing_reviewed_by");
            builder.Property(r => r.HousingReviewedAt).HasColumnName("housing_reviewed_at");
            builder.Property(r => r.HousingNotes).HasColumnName("housing_notes");
            builder.Property(r => r.CyberReviewedBy).HasColumnName("cyber_reviewed_by");
            builder.Property(r => r.CyberReviewedAt).HasColumnName("cyber_reviewed_at");
            builder.Property(r => r.CyberNotes).HasColumnName("cyber_notes");
            builder.Property(r => r.ReadyForProvisioningAt).HasColumnName("ready_for_provisioning_at");
            builder.Property(r => r.ReadyForProvisioningBy).HasColumnName("ready_for_provisioning_by");
            builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
            builder.Property(r => r.CompletedBy).HasColumnName("completed_by");
            builder.Property(r => r.BulkRequestId).HasColumnName("bulk_request_id");
            builder.Property(r => r.RequestNumber).HasColumnName("request_number");
            builder.Property(r => r.RegistrationData).HasColumnName("registration_data");

            // فهارس العدّادات والطوابير (dashboard/queues بتفلتر بالحالة والنوع)
            builder.HasIndex(r => r.Status)
                .HasDatabaseName("IX_Requests_status");

            builder.HasIndex(r => new { r.RequestType, r.Status })
                .HasDatabaseName("IX_Requests_type_status");

            builder.HasIndex(r => r.RequestNumber)
                .IsUnique()
                .HasDatabaseName("IX_Requests_request_number")
                .HasFilter("[request_number] IS NOT NULL");

            builder.HasOne(r => r.Student)
                .WithMany()
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
