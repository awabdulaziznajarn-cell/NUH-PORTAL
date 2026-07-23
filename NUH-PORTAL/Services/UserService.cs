using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Users;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // إدارة المستخدمين عبر ASP.NET Identity (UserManager/RoleManager) + إضافة من الـ AD.
    public class UserService : AppServiceBase, IUserService
    {
        private readonly IRepository<User> _users;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly ActiveDirectoryService _adService;
        private readonly IRepository<AuditLog> _auditLogs;
        private readonly IHttpContextAccessor _http;

        public UserService(
            IRepository<User> users,
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            ActiveDirectoryService adService,
            IRepository<AuditLog> auditLogs,
            IHttpContextAccessor http,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _users = users;
            _userManager = userManager;
            _roleManager = roleManager;
            _adService = adService;
            _auditLogs = auditLogs;
            _http = http;
        }

        // ----------------------------- Queries -----------------------------

        public async Task<List<UserListItemDto>> GetUsersAsync()
        {
            return await _users.Query().AsNoTracking()
                .OrderByDescending(u => u.created_at)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    username = u.UserName,
                    full_name = u.full_name,
                    email = u.Email,
                    role = u.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault(),
                    created_at = u.created_at,
                    is_active = u.is_active
                })
                .ToListAsync();
        }

        public async Task<UserListItemDto> GetUserAsync(int id)
        {
            var user = await _users.Query().AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    username = u.UserName,
                    full_name = u.full_name,
                    email = u.Email,
                    role = u.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault(),
                    created_at = u.created_at,
                    is_active = u.is_active
                })
                .FirstOrDefaultAsync();

            return user ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");
        }

        public async Task<UserDetailDto> GetUserDetailAsync(int id)
        {
            var dto = await _users.Query().AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new UserDetailDto
                {
                    Id = u.Id,
                    username = u.UserName,
                    full_name = u.full_name,
                    email = u.Email,
                    mobile = u.mobile,
                    department = u.department,
                    job_title = u.job_title,
                    role = u.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault(),
                    is_active = u.is_active,
                    created_at = u.created_at
                })
                .FirstOrDefaultAsync();

            return dto ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");
        }

        public async Task<List<RoleOptionDto>> GetRolesAsync()
        {
            return await _roleManager.Roles.AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new RoleOptionDto { name = r.Name!, description = r.Description })
                .ToListAsync();
        }

        // ----------------------------- Commands -----------------------------

        public async Task<UserDetailDto> CreateAsync(UserCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.username))
                throw new UserFriendlyException("اسم المستخدم مطلوب", 400);
            if (string.IsNullOrWhiteSpace(dto.password))
                throw new UserFriendlyException("كلمة المرور مطلوبة", 400);

            if (await _userManager.FindByNameAsync(dto.username) != null)
                throw new UserFriendlyException("اسم المستخدم مستخدم بالفعل", 409);

            var user = new User
            {
                UserName = dto.username.Trim(),
                full_name = dto.full_name,
                Email = dto.email,
                mobile = dto.mobile,
                department = dto.department,
                job_title = dto.job_title,
                is_active = dto.is_active,
                created_at = DateTime.UtcNow
            };

            var res = await _userManager.CreateAsync(user, dto.password);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر إنشاء المستخدم: " + IdentityErrors(res), 400);

            var role = string.IsNullOrWhiteSpace(dto.role) ? "user" : dto.role.Trim().ToLowerInvariant();
            await AssignRoleInternalAsync(user, role);

            await AddAuditAsync("user_created", user.Id);
            await UnitOfWork.SaveAsync();

            return await GetUserDetailAsync(user.Id);
        }

        public async Task<UserDetailDto> UpdateAsync(int id, UserUpdateDto dto)
        {
            var user = await _userManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            user.full_name = dto.full_name;
            user.Email = dto.email;
            user.mobile = dto.mobile;
            user.department = dto.department;
            user.job_title = dto.job_title;
            user.is_active = dto.is_active;

            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر تعديل المستخدم: " + IdentityErrors(res), 400);

            if (!string.IsNullOrWhiteSpace(dto.role))
                await AssignRoleInternalAsync(user, dto.role.Trim().ToLowerInvariant());

            if (!string.IsNullOrWhiteSpace(dto.password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var pwdRes = await _userManager.ResetPasswordAsync(user, token, dto.password);
                if (!pwdRes.Succeeded)
                    throw new UserFriendlyException("تعذّر تعيين كلمة المرور: " + IdentityErrors(pwdRes), 400);
            }

            await AddAuditAsync("user_updated", user.Id);
            await UnitOfWork.SaveAsync();

            return await GetUserDetailAsync(user.Id);
        }

        public async Task SetActiveAsync(int id, bool active)
        {
            var user = await _userManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            user.is_active = active;
            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر تحديث حالة المستخدم: " + IdentityErrors(res), 400);

            await AddAuditAsync(active ? "user_activated" : "user_deactivated", user.Id);
            await UnitOfWork.SaveAsync();
        }

        public async Task AssignRoleAsync(int id, string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new UserFriendlyException("الدور مطلوب", 400);

            var user = await _userManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            await AssignRoleInternalAsync(user, role.Trim().ToLowerInvariant());
            await AddAuditAsync("user_role_assigned", user.Id);
            await UnitOfWork.SaveAsync();
        }

        // ----------------------------- LDAP -----------------------------

        public async Task<List<LdapUserSearchItemDto>> SearchLdapAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                throw new UserFriendlyException("اكتب حرفين على الأقل للبحث", 400);

            var res = await _adService.SearchUsersAsync(query.Trim(), 25);
            if (!res.Success)
                throw new UserFriendlyException(res.Error ?? "تعذّر الاتصال بالدليل (Active Directory)", 400);

            return res.Users.Select(u => new LdapUserSearchItemDto
            {
                samAccountName = u.SamAccountName,
                displayName = u.DisplayName,
                email = u.Email,
                department = u.Department,
                accountEnabled = u.AccountEnabled
            }).ToList();
        }

        public async Task<UserDetailDto> AddFromLdapAsync(LdapAddUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.username))
                throw new UserFriendlyException("اسم الحساب مطلوب", 400);

            var sam = dto.username.Trim();
            if (await _userManager.FindByNameAsync(sam) != null)
                throw new UserFriendlyException("المستخدم موجود بالفعل في النظام", 409);

            var ad = await _adService.GetUserBySamAccountNameAsync(sam);
            if (!ad.Success)
                throw new UserFriendlyException(ad.Error ?? "تعذّر إيجاد المستخدم في الدليل", 400);

            var user = new User
            {
                UserName = ad.SamAccountName,
                full_name = string.IsNullOrWhiteSpace(ad.DisplayName)
                    ? (ad.GivenName + " " + ad.Surname).Trim()
                    : ad.DisplayName,
                Email = AdAttr(ad, "mail"),
                department = ad.Department,
                job_title = AdAttr(ad, "title"),
                mobile = AdAttr(ad, "mobile") ?? AdAttr(ad, "telephoneNumber"),
                is_active = true,
                created_at = DateTime.UtcNow
            };

            // مستخدم AD من غير باسورد محلي — الدخول عبر AD
            var res = await _userManager.CreateAsync(user);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر إضافة المستخدم من الدليل: " + IdentityErrors(res), 400);

            var role = string.IsNullOrWhiteSpace(dto.role) ? "user" : dto.role.Trim().ToLowerInvariant();
            await AssignRoleInternalAsync(user, role);

            await AddAuditAsync("user_added_from_ldap", user.Id);
            await UnitOfWork.SaveAsync();

            return await GetUserDetailAsync(user.Id);
        }

        // ----------------------------- Helpers -----------------------------

        private async Task AssignRoleInternalAsync(User user, string role)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new Role(role) { Description = role });

            var current = await _userManager.GetRolesAsync(user);
            if (current.Count > 0)
                await _userManager.RemoveFromRolesAsync(user, current);

            await _userManager.AddToRoleAsync(user, role);
        }

        private static string? AdAttr(ADReadUserResult r, string key)
            => r.AttributesRaw != null && r.AttributesRaw.TryGetValue(key, out var v) && v.Count > 0 ? v[0] : null;

        private static string IdentityErrors(IdentityResult res)
            => string.Join("، ", res.Errors.Select(e => e.Description));

        private async Task AddAuditAsync(string action, int targetId)
        {
            var actorId = UnitOfWork.GetCurrentUserId();
            var ctx = _http.HttpContext;
            await _auditLogs.AddAsync(new AuditLog
            {
                user_id = actorId > 0 ? actorId : null,
                action = action,
                target_table = "Users",
                target_id = targetId,
                action_at = DateTime.UtcNow,
                ip_address = ctx?.Connection.RemoteIpAddress?.ToString(),
                user_agent = ctx?.Request.Headers.UserAgent.ToString() ?? ""
            });
        }
    }
}
