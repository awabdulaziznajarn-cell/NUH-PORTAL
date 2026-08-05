using Microsoft.EntityFrameworkCore.Storage;

namespace NUH_PORTAL.Data.Interfaces
{
    // وحدة العمل: الحفظ + هوية/دور المستخدم الحالي (من الـ JWT) + المعاملات
    public interface IUnitOfWork
    {
        Task<bool> SaveAsync();
        int GetCurrentUserId();
        string? GetCurrentUserRole();
        // فحص صلاحية المستخدم الحالي جوّه الـ service. الفحص على مستوى الكنترولر
        // بيقفل الـ endpoint كله، لكن في حالات القرار بيعتمد على مرحلة الطلب —
        // زي اعتماد طلب: مين يعتمد بيتحدّد من مرحلته مش من مين طالب الاعتماد.
        bool HasPermission(string permission);
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
