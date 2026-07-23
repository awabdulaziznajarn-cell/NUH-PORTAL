using NUH_PORTAL.DTOs.Users;

namespace NUH_PORTAL.Services.Interfaces
{
    // منطق المستخدمين — قراءة + إدارة (إنشاء/تعديل/تفعيل/إسناد دور) + إضافة من الـ AD.
    public interface IUserService
    {
        Task<List<UserListItemDto>> GetUsersAsync();
        Task<UserListItemDto> GetUserAsync(int id);
        Task<UserDetailDto> GetUserDetailAsync(int id);

        Task<UserDetailDto> CreateAsync(UserCreateDto dto);
        Task<UserDetailDto> UpdateAsync(int id, UserUpdateDto dto);
        Task SetActiveAsync(int id, bool active);
        Task AssignRoleAsync(int id, string role);

        Task<List<RoleOptionDto>> GetRolesAsync();

        // إضافة من الدليل (Active Directory)
        Task<List<LdapUserSearchItemDto>> SearchLdapAsync(string query);
        Task<UserDetailDto> AddFromLdapAsync(LdapAddUserDto dto);
    }
}
