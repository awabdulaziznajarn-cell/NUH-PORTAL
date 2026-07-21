using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");
            builder.HasKey(n => n.Id);

            // جرس الإشعارات بيسأل بالدور + الحالة في كل صفحة
            builder.HasIndex(n => new { n.recipient_role, n.status })
                .HasDatabaseName("IX_Notifications_role_status");
        }
    }
}
