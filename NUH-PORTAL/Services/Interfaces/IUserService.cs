using NUH_PORTAL.DTOs.Users;

namespace NUH_PORTAL.Services.Interfaces
{
    // منطق المستخدمين (قراءة فقط حاليًا — زي الكنترولر القديم)
    public interface IUserService
    {
        Task<List<UserListItemDto>> GetUsersAsync();
        Task<UserListItemDto> GetUserAsync(int id);
    }
}
