using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Student> Students { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AuditChangeLog> AuditChangeLogs { get; set; }
        public DbSet<BulkRequest> BulkRequests { get; set; }
        public DbSet<BulkRequestStudent> BulkRequestStudents { get; set; }
        public DbSet<StudentStatusAction> StudentStatusActions { get; set; }
        public DbSet<AccountLifecycleLog> AccountLifecycleLogs { get; set; }
        public DbSet<ADConfiguration> ADConfigurations { get; set; }
        public DbSet<OTPVerification> OTPVerifications { get; set; }
        public DbSet<StudentDeclaration> StudentDeclarations { get; set; }
        public DbSet<WorkflowHistory> WorkflowHistories { get; set; }
        public DbSet<SMSLog> SMSLogs { get; set; }
        public DbSet<RequestAttachment> RequestAttachments { get; set; }
        public DbSet<StudentStatusAttachment> StudentStatusAttachments { get; set; }
        public DbSet<HousingTransfer> HousingTransfers { get; set; }

        // قوائم مرجعية (lookups) + بنود التعهّد
        public DbSet<College> Colleges { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Building> Buildings { get; set; }
        public DbSet<AcademicLevel> AcademicLevels { get; set; }
        public DbSet<Term> Terms { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // كل إعدادات الـ Fluent API لكل entity في ملف منفصل تحت Data/Configurations
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
