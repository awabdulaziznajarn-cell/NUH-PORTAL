namespace NUH_PORTAL.Data.Interfaces
{
    // وحدة العمل: الحفظ + هوية المستخدم الحالي (من الـ JWT)
    public interface IUnitOfWork
    {
        Task<bool> SaveAsync();
        int GetCurrentUserId();
    }
}
