using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.DTOs.Roles;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;
using System.Security.Claims;

namespace NUH_PORTAL.Services
{
    // إدارة الأدوار وصلاحياتها. الصلاحيات بتتخزّن كـ claims على الدور (نوع "permission").
    // ملاحظة: عمليات RoleManager بتحفظ فورًا (store بيعمل SaveChanges) — مش محتاجة UnitOfWork.
    public class RoleAdminService : IRoleAdminService
    {
        private readonly RoleManager<Role> _roleManager;
        private readonly IRepository<UserRole> _userRoles;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

        // الأدوار الأساسية اللي مينفعش تتحذف (بيعتمد عليها الزرع والتدفقات).
        private static readonly HashSet<string> ProtectedRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "admin", "user"
        };

        // مجموعة قيم الصلاحيات الصحيحة — أي قيمة برّه الكتالوج بتتجاهل.
        private static readonly HashSet<string> ValidPermissions =
            ApplicationPermissions.All.Select(p => p.Value).ToHashSet();

        public RoleAdminService(RoleManager<Role> roleManager, IRepository<UserRole> userRoles,
                                Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
        {
            _roleManager = roleManager;
            _userRoles = userRoles;
            _cache = cache;
        }

        public async Task<List<RoleListItemDto>> GetRolesAsync()
        {
            var roles = await _roleManager.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();

            // عدد المستخدمين لكل دور في استعلام واحد
            var counts = await _userRoles.Query().AsNoTracking()
                .GroupBy(ur => ur.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count);

            var list = new List<RoleListItemDto>();
            foreach (var r in roles)
            {
                var claims = await _roleManager.GetClaimsAsync(r);
                list.Add(new RoleListItemDto
                {
                    id = r.Id,
                    name = r.Name ?? "",
                    description = r.Description,
                    userCount = counts.TryGetValue(r.Id, out var c) ? c : 0,
                    permissionCount = claims.Count(cl => cl.Type == ClaimConstants.Permission)
                });
            }
            return list;
        }

        public async Task<RoleDetailDto> GetRoleAsync(int id)
        {
            var role = await _roleManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("الدور غير موجود");

            var claims = await _roleManager.GetClaimsAsync(role);
            return new RoleDetailDto
            {
                id = role.Id,
                name = role.Name ?? "",
                description = role.Description,
                permissions = claims.Where(c => c.Type == ClaimConstants.Permission).Select(c => c.Value).ToList()
            };
        }

        public List<PermissionGroupDto> GetPermissionCatalog()
        {
            return ApplicationPermissions.All
                .GroupBy(p => p.GroupName)
                .Select(g => new PermissionGroupDto
                {
                    groupName = g.Key,
                    permissions = g.Select(p => new PermissionItemDto
                    {
                        value = p.Value,
                        name = p.Name,
                        description = p.Description
                    }).ToList()
                })
                .ToList();
        }

        public async Task<RoleDetailDto> CreateRoleAsync(RoleSaveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.name))
                throw new UserFriendlyException("اسم الدور مطلوب", 400);

            var name = dto.name.Trim().ToLowerInvariant();
            if (await _roleManager.RoleExistsAsync(name))
                throw new UserFriendlyException("اسم الدور مستخدم بالفعل", 409);

            var role = new Role(name) { Description = dto.description };
            var res = await _roleManager.CreateAsync(role);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر إنشاء الدور: " + Errors(res), 400);

            await SyncPermissionsAsync(role, dto.permissions);
            return await GetRoleAsync(role.Id);
        }

        public async Task<RoleDetailDto> UpdateRoleAsync(int id, RoleSaveDto dto)
        {
            var role = await _roleManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("الدور غير موجود");

            // الاسم ثابت (مفتاح مرجعي في التوكن/الكوكي) — بنعدّل الوصف والصلاحيات فقط
            role.Description = dto.description;
            var res = await _roleManager.UpdateAsync(role);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر تعديل الدور: " + Errors(res), 400);

            await SyncPermissionsAsync(role, dto.permissions);
            return await GetRoleAsync(role.Id);
        }

        public async Task DeleteRoleAsync(int id)
        {
            var role = await _roleManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("الدور غير موجود");

            if (ProtectedRoles.Contains(role.Name ?? ""))
                throw new UserFriendlyException("لا يمكن حذف دور أساسي في النظام", 400);

            var inUse = await _userRoles.Query().AnyAsync(ur => ur.RoleId == id);
            if (inUse)
                throw new UserFriendlyException("لا يمكن حذف دور مُسند لمستخدمين - انقل المستخدمين لدور آخر أولًا", 400);

            var res = await _roleManager.DeleteAsync(role);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر حذف الدور: " + Errors(res), 400);
        }

        // ----------------------------- Helpers -----------------------------

        // يوفّق claims الدور مع المطلوب: يشيل الزايد ويضيف الناقص (القيم غير الصحيحة تتجاهل).
        private async Task SyncPermissionsAsync(Role role, List<string> requested)
        {
            var desired = (requested ?? new List<string>())
                .Where(ValidPermissions.Contains)
                .ToHashSet();

            var existing = await _roleManager.GetClaimsAsync(role);
            var existingPerms = existing
                .Where(c => c.Type == ClaimConstants.Permission)
                .ToList();
            var existingValues = existingPerms.Select(c => c.Value).ToHashSet();

            foreach (var c in existingPerms.Where(c => !desired.Contains(c.Value)))
                await _roleManager.RemoveClaimAsync(role, c);

            foreach (var p in desired.Where(p => !existingValues.Contains(p)))
                await _roleManager.AddClaimAsync(role, new Claim(ClaimConstants.Permission, p));

            // التعديل يسري على الطلب التالي بلا انتظار انتهاء مدة التخزين المؤقت
            if (!string.IsNullOrWhiteSpace(role.Name))
                PermissionClaimsTransformation.Invalidate(_cache, role.Name);
        }

        private static string Errors(IdentityResult res)
            => string.Join("، ", res.Errors.Select(e => e.Description));
    }
}
