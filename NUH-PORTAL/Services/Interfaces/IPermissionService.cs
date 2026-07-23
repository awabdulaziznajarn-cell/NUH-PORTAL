namespace NUH_PORTAL.Services.Interfaces
{
    // بيحسب صلاحيات المستخدم من أدواره (claims الأدوار) — بتتحمّل في التوكن/الكوكي وقت الدخول.
    public interface IPermissionService
    {
        Task<List<string>> GetPermissionsForRolesAsync(IEnumerable<string> roles);
    }
}
