using Microsoft.EntityFrameworkCore.Storage;

namespace NUH_PORTAL.Data.Interfaces
{
    // وحدة العمل: الحفظ + هوية/دور المستخدم الحالي (من الـ JWT) + المعاملات
    public interface IUnitOfWork
    {
        Task<bool> SaveAsync();
        int GetCurrentUserId();
        string? GetCurrentUserRole();
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
