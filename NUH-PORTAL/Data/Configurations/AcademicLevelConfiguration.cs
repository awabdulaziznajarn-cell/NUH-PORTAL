using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class AcademicLevelConfiguration : IEntityTypeConfiguration<AcademicLevel>
    {
        public void Configure(EntityTypeBuilder<AcademicLevel> builder)
        {
            builder.ToTable("AcademicLevels");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Code).HasMaxLength(50).IsRequired();
            builder.Property(a => a.ArName).HasMaxLength(200).IsRequired();
            builder.Property(a => a.EnName).HasMaxLength(200).IsRequired();
            builder.HasIndex(a => a.Code).IsUnique();
        }
    }
}
