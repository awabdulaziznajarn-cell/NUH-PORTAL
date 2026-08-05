using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class WorkflowHistoryConfiguration : IEntityTypeConfiguration<WorkflowHistory>
    {
        public void Configure(EntityTypeBuilder<WorkflowHistory> builder)
        {
            builder.ToTable("WorkflowHistory");
            builder.HasKey(w => w.Id);

            builder.Property(w => w.RequestId).HasColumnName("request_id");
            builder.Property(w => w.FromStage).HasColumnName("from_stage");
            builder.Property(w => w.ToStage).HasColumnName("to_stage");
            builder.Property(w => w.ActionBy).HasColumnName("action_by");
            builder.Property(w => w.ActionDate).HasColumnName("action_date")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            builder.Property(w => w.Notes).HasColumnName("notes");
            builder.Property(w => w.ChangesJson).HasColumnName("changes_json");

            builder.HasIndex(w => new { w.RequestId, w.ActionDate })
                .HasDatabaseName("IX_WorkflowHistory_request_date");

            builder.HasOne(w => w.Request)
                .WithMany()
                .HasForeignKey(w => w.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(w => w.Actor)
                .WithMany()
                .HasForeignKey(w => w.ActionBy)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
