using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class BulkRequestStudentConfiguration : IEntityTypeConfiguration<BulkRequestStudent>
    {
        public void Configure(EntityTypeBuilder<BulkRequestStudent> builder)
        {
            builder.ToTable("BulkRequestStudents");
            builder.HasKey(b => b.Id);

            builder.HasOne(b => b.BulkRequest)
                .WithMany(r => r.Students)
                .HasForeignKey(b => b.BulkRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
