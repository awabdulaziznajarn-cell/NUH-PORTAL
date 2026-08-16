using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NUH_PORTAL.Core;
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
        private readonly IMemoryCache _cache;

        public UserService(
            IRepository<User> users,
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            ActiveDirectoryService adService,
            IRepository<AuditLog> auditLogs,
            IHttpContextAccessor http,
            IMemoryCache cache,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _users = users;
            _userManager = userManager;
            _roleManager = roleManager;
            _adService = adService;
            _auditLogs = auditLogs;
            _http = http;
            _cache = cache;
        }

        // ----------------------------- Queries -----------------------------

        // اسم دور الطالب. حسابات الطلاب بتتولّد تلقائيًا في OtpFlowService عند
        // التحقق برمز الجوال، فعددها بيكبر مع كل طالب بيقدّم طلب — وشاشة
        // «المستخدمون» شاشة إدارة موظفين، مش سجل طلاب.
        private const string StudentRoleName = "user";

        // دور مدير النظام — بنمنع حذف آخر واحد منه عشان النظام مايتقفلش على الكل
        private const string AdminRoleName = "admin";

        public async Task<UserCountsDto> GetCountsAsync()
        {
            var query = _users.Query().AsNoTracking();

            // المحذوف بيتشال من عدّاد تبويبه الأصلي وبيتحسب في تبويب المحذوفين،
            // وإلا مجموع التبويبات يبقى أكبر من عدد الصفوف الظاهرة فعلًا.
            var deleted = await query.CountAsync(u => u.is_deleted);
            var live = query.Where(u => !u.is_deleted);
            var students = await live.CountAsync(u => u.UserRoles.Any(ur => ur.Role.Name == StudentRoleName));
            var total = await live.CountAsync();

            return new UserCountsDto { Students = students, Staff = total - students, Deleted = deleted };
        }

        public async Task<List<UserListItemDto>> GetUsersAsync(bool studentsOnly = false, bool showDeleted = false)
        {
            var query = _users.Query().AsNoTracking();

            // تبويب المحذوفين بيعرض كل المحذوفين مع بعض (موظفين وطلاب) — التقسيم
            // لموظف/طالب مالوش معنى هنا، اللي يهم إنه مقفول ومحتاج قرار استعادة.
            if (showDeleted)
            {
                query = query.Where(u => u.is_deleted);
            }
            else
            {
                query = query.Where(u => !u.is_deleted);
                query = studentsOnly
                    ? query.Where(u => u.UserRoles.Any(ur => ur.Role.Name == StudentRoleName))
                    : query.Where(u => !u.UserRoles.Any(ur => ur.Role.Name == StudentRoleName));
            }

            return await query
                .OrderByDescending(u => u.created_at)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    username = u.UserName,
                    full_name = u.full_name,
                    email = u.Email,
                    role = u.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault(),
                    mobile = u.mobile,
                    created_at = u.created_at,
                    is_active = u.is_active,
                    is_deleted = u.is_deleted,
                    deleted_at = u.deleted_at,
                    auth_source = u.auth_source,
                    scope_gender = u.scope_gender
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
                    is_deleted = u.is_deleted,
                    auth_source = u.auth_source,
                    scope_gender = u.scope_gender,
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

            // ⚠️ المحذوف صفّه لسه في الجدول، فاسمه لسه محجوز. من غير الرسالة دي
            //    المسؤول يشوف «الاسم مستخدم بالفعل» وهو مش شايف الحساب في الشاشة.
            var duplicate = await _userManager.FindByNameAsync(dto.username);
            if (duplicate != null)
                throw new UserFriendlyException(duplicate.is_deleted
                    ? "الحساب موجود ضمن المحذوفين — استعِده من تبويب «المحذوفون» بدل إنشائه من جديد"
                    : "اسم المستخدم مستخدم بالفعل", 409);

            var user = new User
            {
                UserName = dto.username.Trim(),
                full_name = dto.full_name,
                Email = dto.email,
                mobile = NullIfBlank(dto.mobile),
                department = dto.department,
                job_title = dto.job_title,
                is_active = dto.is_active,
                created_at = DateTime.UtcNow,
                auth_source = "local",
                scope_gender = dto.scope_gender
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
            user.mobile = NullIfBlank(dto.mobile);
            user.department = dto.department;
            user.job_title = dto.job_title;
            user.is_active = dto.is_active;
            user.scope_gender = dto.scope_gender;

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

            // ⚠️ القسم متخزّن في الـ claims بكاش دقيقة (PermissionClaimsTransformation).
            //    من غير الإبطال ده المسؤول يغيّر قسم المشرفة ويقولها جرّبي، فتلاقي
            //    نفس الشاشة القديمة وتفتكر إن التعديل ماحصلش.
            PermissionClaimsTransformation.InvalidateScope(_cache, user.Id);

            return await GetUserDetailAsync(user.Id);
        }

        public async Task SetActiveAsync(int id, bool active)
        {
            var user = await _userManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            // الحساب المحذوف بيتستعاد الأول، وبعدها يتفعّل — قرارين منفصلين عن قصد
            if (user.is_deleted)
                throw new UserFriendlyException("الحساب محذوف — استعِده أولًا من تبويب «المحذوفون»", 400);

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

        // ⚠️ حذف منطقي: is_deleted = 1 والصف بيفضل مكانه.
        //    الحذف النهائي كان هيمسح اسم الفاعل من كل سطر في سجل الإجراءات عمله
        //    الموظف ده (AuditLogs بتربط بالـ id والعلاقة OnDelete: SetNull)، وكمان
        //    مكانش هيمنعه فعليًا: أول ما يسجّل دخول بالدومين تاني كان SyncAdUserAsync
        //    هيعمله حساب جديد تلقائي. الحذف المنطقي بيقفل البابين.
        public async Task DeleteAsync(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            if (user.is_deleted)
                throw new UserFriendlyException("الحساب محذوف بالفعل", 400);

            var actorId = UnitOfWork.GetCurrentUserId();

            // من غير الشرط ده المسؤول يقدر يحذف نفسه وهو داخل: الجلسة بتفضل شغّالة
            // لحد ما تنتهي، وبعدها مايقدرش يدخل — ولا حد يقدر يرجّعه لو كان آخر أدمن.
            if (actorId == user.Id)
                throw new UserFriendlyException("لا يمكنك حذف حسابك الشخصي", 400);

            // لازم يفضل حساب مدير نظام نشط واحد على الأقل، وإلا الشاشة تتقفل على الكل
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Any(r => string.Equals(r, AdminRoleName, StringComparison.OrdinalIgnoreCase)))
            {
                var otherAdmins = await _users.Query().AsNoTracking()
                    .CountAsync(u => u.Id != user.Id && !u.is_deleted && u.is_active
                                  && u.UserRoles.Any(ur => ur.Role.Name == AdminRoleName));
                if (otherAdmins == 0)
                    throw new UserFriendlyException("لا يمكن حذف آخر حساب مدير نظام نشط في النظام", 400);
            }

            user.is_deleted = true;
            user.is_active = false;
            user.deleted_at = DateTime.UtcNow;
            user.deleted_by = actorId > 0 ? actorId : null;

            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر حذف المستخدم: " + IdentityErrors(res), 400);

            await AddAuditAsync("user_deleted", user.Id);
            await UnitOfWork.SaveAsync();
        }

        public async Task RestoreAsync(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            if (!user.is_deleted)
                throw new UserFriendlyException("الحساب غير محذوف", 400);

            user.is_deleted = false;
            user.deleted_at = null;
            user.deleted_by = null;
            // ⚠️ الاستعادة بترجّع الحساب للقائمة معطّلًا عن قصد. لو رجع نشط على طول
            //    يبقى ضغطة واحدة بالغلط رجّعت حساب للخدمة من غير ما حد ياخد باله.
            //    التفعيل قرار تاني بزرار «تفعيل».

            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر استعادة المستخدم: " + IdentityErrors(res), 400);

            await AddAuditAsync("user_restored", user.Id);
            await UnitOfWork.SaveAsync();
        }

        // ----------------------------- LDAP -----------------------------

        public async Task<List<LdapUserSearchItemDto>> SearchLdapAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                throw new UserFriendlyException("يرجى إدخال حرفين على الأقل للبحث", 400);

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
            var duplicate = await _userManager.FindByNameAsync(sam);
            if (duplicate != null)
                throw new UserFriendlyException(duplicate.is_deleted
                    ? "الحساب موجود ضمن المحذوفين — استعِده من تبويب «المحذوفون» بدل إضافته من جديد"
                    : "المستخدم موجود بالفعل في النظام", 409);

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
                mobile = NullIfBlank(AdAttr(ad, "mobile") ?? AdAttr(ad, "telephoneNumber")),
                is_active = true,
                created_at = DateTime.UtcNow,
                auth_source = "ad",
                scope_gender = dto.scope_gender
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

        // ⚠️ الجوال عليه فهرس فريد مُصفّى: UNIQUE WHERE [mobile] IS NOT NULL.
        //    الفلتر بيستثني NULL بس — والنص الفاضي "" مش NULL، فبيدخل الفهرس
        //    عادي. يعني تاني موظف يتحفظ وخانة الجوال فاضية بيصطدم بالأول
        //    ويرجّع خطأ قاعدة بيانات غامض: «القيمة مكرّرة... duplicate key value is ()».
        //    الواجهة بتبعت "" لما الخانة فاضية، فالتطبيع لازم يحصل هنا: فاضي = NULL.
        private static string? NullIfBlank(string? v)
            => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

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
