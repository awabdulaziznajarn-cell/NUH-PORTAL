using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class StudentStatusActionConfiguration : IEntityTypeConfiguration<StudentStatusAction>
    {
        public void Configure(EntityTypeBuilder<StudentStatusAction> builder)
        {
            builder.ToTable("StudentStatusActions");
            builder.HasKey(s => s.Id);

            builder.HasOne(s => s.Student)
                .WithMany()
                .HasForeignKey(s => s.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.CreatedByUser)
                .WithMany()
                .HasForeignKey(s => s.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
