using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class ADConfigurationConfiguration : IEntityTypeConfiguration<ADConfiguration>
    {
        public void Configure(EntityTypeBuilder<ADConfiguration> builder)
        {
            builder.ToTable("ADConfiguration");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.ConfigKey).HasColumnName("config_key");
            builder.Property(a => a.ConfigValue).HasColumnName("config_value");
            builder.Property(a => a.Description).HasColumnName("description");
            builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
            builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(a => a.ConfigKey)
                .IsUnique()
                .HasDatabaseName("IX_ADConfiguration_config_key")
                .HasFilter("[config_key] IS NOT NULL");

            builder.HasOne(a => a.UpdatedByUser)
                .WithMany()
                .HasForeignKey(a => a.UpdatedBy)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
