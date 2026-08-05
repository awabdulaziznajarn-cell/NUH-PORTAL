using NUH_PORTAL.DTOs.Users;

namespace NUH_PORTAL.Services.Interfaces
{
    // منطق المستخدمين — قراءة + إدارة (إنشاء/تعديل/تفعيل/إسناد دور) + إضافة من الـ AD.
    public interface IUserService
    {
        // ⚠️ الطلاب بيتعمل لهم حساب تلقائيًا أول ما يتحققوا برمز الجوال — مش موظفين
        //    وماحدش بيديرهم من الشاشة دي، فالشاشة اتقسمت تبويبين:
        //    studentsOnly=false → الموظفين، studentsOnly=true → حسابات الطلاب.
        Task<List<UserListItemDto>> GetUsersAsync(bool studentsOnly = false);
        Task<UserCountsDto> GetCountsAsync();
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
