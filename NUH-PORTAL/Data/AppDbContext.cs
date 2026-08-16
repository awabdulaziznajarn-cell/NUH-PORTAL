using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Data
{
    // IdentityDbContext بيوفّر Users/Roles + جداول Identity (Claims/UserRoles/Tokens...).
    // بنستخدم UserRole مخصّص عشان نضيف navigations (User/Role) للاستعلامات.
    public class AppDbContext : IdentityDbContext<User, Role, int, IdentityUserClaim<int>, UserRole, IdentityUserLogin<int>, IdentityRoleClaim<int>, IdentityUserToken<int>>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Student> Students { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AuditChangeLog> AuditChangeLogs { get; set; }
        public DbSet<ErrorLog> ErrorLogs { get; set; }
        public DbSet<SignInLog> SignInLogs { get; set; }
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

        // سكن أعضاء هيئة التدريس — الوحدة ثابتة (FacultyUnit) والساكن متغيّر
        // (FacultyOccupancy صف لكل فترة إشغال). منفصلين تمامًا عن سكن الطلاب.
        public DbSet<FacultyUnit> FacultyUnits { get; set; }
        public DbSet<FacultyOccupancy> FacultyOccupancies { get; set; }

        // قوائم مرجعية (lookups) + بنود التعهّد
        public DbSet<College> Colleges { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Building> Buildings { get; set; }
        public DbSet<AcademicLevel> AcademicLevels { get; set; }
        public DbSet<Term> Terms { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // لازم الأول عشان Identity يظبّط جداوله
            base.OnModelCreating(modelBuilder);

            // navigations للـ UserRole (User <-> Role) عشان نقرأ الأدوار في الاستعلامات
            modelBuilder.Entity<UserRole>(b =>
            {
                b.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId).IsRequired();
                b.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId).IsRequired();
            });

            // كل إعدادات الـ Fluent API لكل entity في ملف منفصل تحت Data/Configurations
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
