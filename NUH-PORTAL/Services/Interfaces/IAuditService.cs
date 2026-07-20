using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services.Interfaces
{
    // تسجيل الأحداث (audit) — قابل لإعادة الاستخدام عبر كل الـ services
    public interface IAuditService
    {
        Task LogAsync(string action, string targetTable, int targetId, List<AuditChangeLog>? changes = null);
    }
}
