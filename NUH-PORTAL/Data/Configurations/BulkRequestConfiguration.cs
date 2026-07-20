using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class BulkRequestConfiguration : IEntityTypeConfiguration<BulkRequest>
    {
        public void Configure(EntityTypeBuilder<BulkRequest> builder)
        {
            builder.ToTable("BulkRequests");
            builder.HasKey(b => b.Id);
        }
    }
}
