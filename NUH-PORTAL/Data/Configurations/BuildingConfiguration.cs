using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Data.Converters;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class BuildingConfiguration : IEntityTypeConfiguration<Building>
    {
        public void Configure(EntityTypeBuilder<Building> builder)
        {
            builder.ToTable("Buildings");
            builder.HasKey(b => b.Id);
            builder.Property(b => b.Code).HasMaxLength(50).IsRequired();
            builder.Property(b => b.ArName).HasMaxLength(200).IsRequired();
            builder.Property(b => b.EnName).HasMaxLength(200).IsRequired();
            builder.Property(b => b.Gender).HasConversion(new GenderConverter()).HasMaxLength(20);
            // ⚠️ نصّ لا رقم: صفّ المبنى بيتقرا من SSMS كتير وقت التأسيس،
            //    و"PerFloor" بتقول نفسها بينما "2" محتاجة حد يفتح الكود.
            builder.Property(b => b.Numbering).HasConversion<string>().HasMaxLength(20);
            builder.HasIndex(b => b.Code).IsUnique();
            builder.HasIndex(b => b.Gender).HasDatabaseName("IX_Buildings_gender");
        }
    }
}
