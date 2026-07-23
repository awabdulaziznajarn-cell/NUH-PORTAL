using NUH_PORTAL.DTOs.Roles;

namespace NUH_PORTAL.Services.Interfaces
{
    // إدارة الأدوار وصلاحياتها (الصلاحيات = role claims).
    public interface IRoleAdminService
    {
        Task<List<RoleListItemDto>> GetRolesAsync();
        Task<RoleDetailDto> GetRoleAsync(int id);
        List<PermissionGroupDto> GetPermissionCatalog();
        Task<RoleDetailDto> CreateRoleAsync(RoleSaveDto dto);
        Task<RoleDetailDto> UpdateRoleAsync(int id, RoleSaveDto dto);
        Task DeleteRoleAsync(int id);
    }
}
