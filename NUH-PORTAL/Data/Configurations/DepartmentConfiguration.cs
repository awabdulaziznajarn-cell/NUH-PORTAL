using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
    {
        public void Configure(EntityTypeBuilder<Department> builder)
        {
            builder.ToTable("Departments");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Code).HasMaxLength(50).IsRequired();
            builder.Property(d => d.ArName).HasMaxLength(200).IsRequired();
            builder.Property(d => d.EnName).HasMaxLength(200).IsRequired();
            builder.HasIndex(d => d.Code).IsUnique();

            builder.HasOne(d => d.College)
                .WithMany(c => c.Departments)
                .HasForeignKey(d => d.CollegeId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
