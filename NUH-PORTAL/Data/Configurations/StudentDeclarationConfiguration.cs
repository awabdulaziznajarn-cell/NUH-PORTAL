using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class StudentDeclarationConfiguration : IEntityTypeConfiguration<StudentDeclaration>
    {
        public void Configure(EntityTypeBuilder<StudentDeclaration> builder)
        {
            builder.ToTable("StudentDeclarations");
            builder.HasKey(d => d.Id);

            builder.Property(d => d.RequestId).HasColumnName("request_id");
            builder.Property(d => d.DeclarationAccepted).HasColumnName("declaration_accepted");
            builder.Property(d => d.PolicyAccepted).HasColumnName("policy_accepted");
            builder.Property(d => d.PolicyVersion).HasColumnName("policy_version");
            builder.Property(d => d.AcceptedDate).HasColumnName("accepted_date")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            builder.Property(d => d.IPAddress).HasColumnName("ip_address");
            builder.Property(d => d.UserAgent).HasColumnName("user_agent");

            builder.HasOne(d => d.Request)
                .WithMany()
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
