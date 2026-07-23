using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class CollegeConfiguration : IEntityTypeConfiguration<College>
    {
        public void Configure(EntityTypeBuilder<College> builder)
        {
            builder.ToTable("Colleges");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
            builder.Property(c => c.ArName).HasMaxLength(200).IsRequired();
            builder.Property(c => c.EnName).HasMaxLength(200).IsRequired();
            builder.HasIndex(c => c.Code).IsUnique();
        }
    }
}
