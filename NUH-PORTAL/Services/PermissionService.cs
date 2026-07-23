using Microsoft.AspNetCore.Identity;
using NUH_PORTAL.Core;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // بيجمع صلاحيات المستخدم من كل أدواره (claims من نوع permission) بدون تكرار.
    public class PermissionService : IPermissionService
    {
        private readonly RoleManager<Role> _roleManager;

        public PermissionService(RoleManager<Role> roleManager) => _roleManager = roleManager;

        public async Task<List<string>> GetPermissionsForRolesAsync(IEnumerable<string> roles)
        {
            var perms = new HashSet<string>();
            foreach (var roleName in roles.Distinct())
            {
                var role = await _roleManager.FindByNameAsync(roleName);
                if (role == null) continue;

                var claims = await _roleManager.GetClaimsAsync(role);
                foreach (var c in claims)
                    if (c.Type == ClaimConstants.Permission)
                        perms.Add(c.Value);
            }
            return perms.ToList();
        }
    }
}
