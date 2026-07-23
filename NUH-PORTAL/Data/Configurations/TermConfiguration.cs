using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class TermConfiguration : IEntityTypeConfiguration<Term>
    {
        public void Configure(EntityTypeBuilder<Term> builder)
        {
            builder.ToTable("Terms");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.ArText).HasMaxLength(1000).IsRequired();
            builder.Property(t => t.EnText).HasMaxLength(1000).IsRequired();
        }
    }
}
