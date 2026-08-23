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

        // ⚠️ حساب دخول الطالب بيتعرف **باسم المستخدم** لا بالدور.
        //
        //    كان التقسيم: «عنده دور user ← طالب». وده غلط من أصله — دور "user"
        //    دور قراءة فقط لأي موظف ومالوش علاقة بالطلاب. فكان أي موظف
        //    قراءة-فقط يقع في تبويب حسابات الطلاب، وكذلك أي حساب يتضاف من
        //    الدليل من غير ما المضيف يختار له دورًا (الافتراضي "user").
        //    وده اللي حصل مع fhalharthi.nuh.
        //
        //    كل حساب دخول طالب اسمه بيتولّد في StudentLoginIdentity بالشكل
        //    "student_" + الرقم الجامعي أو الجوال. فالبادئة هي العلامة الوحيدة
        //    المؤكّدة، ومصدرها الثابت هناك لا نسخة مكتوبة هنا.
        //
        //    ⚠️ Expression لا دالة bool: EF لازم يترجمه لـ SQL، وإلا اتنفّذ في
        //       الذاكرة على كل صفوف الجدول.
        private static readonly System.Linq.Expressions.Expression<Func<User, bool>> IsStudentAccount =
            u => u.UserName != null && u.UserName.StartsWith(StudentLoginIdentity.Prefix);

        // دور مدير النظام — بنمنع حذف آخر واحد منه عشان النظام مايتقفلش على الكل
        private const string AdminRoleName = "admin";

        // نفي IsStudentAccount مبنيّ منه لا مكتوب بالإيد — فأي تعديل في تعريف
        // الطالب بينعكس على التبويبين معًا ومستحيل يفترقا.
        private static readonly System.Linq.Expressions.Expression<Func<User, bool>> NotStudentAccount =
            System.Linq.Expressions.Expression.Lambda<Func<User, bool>>(
                System.Linq.Expressions.Expression.Not(IsStudentAccount.Body),
                IsStudentAccount.Parameters);

        public async Task<UserCountsDto> GetCountsAsync()
        {
            var query = _users.Query().AsNoTracking();

            // المحذوف بيتشال من عدّاد تبويبه الأصلي وبيتحسب في تبويب المحذوفين،
            // وإلا مجموع التبويبات يبقى أكبر من عدد الصفوف الظاهرة فعلًا.
            var deleted = await query.CountAsync(u => u.is_deleted);
            var live = query.Where(u => !u.is_deleted);
            var students = await live.CountAsync(IsStudentAccount);
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
                // ⚠️ النفي هنا لازم يكون على نفس التعبير بالحرف، وإلا حساب
                //    يقع في التبويبين أو ما يظهرش في أي تبويب.
                query = studentsOnly
                    ? query.Where(IsStudentAccount)
                    : query.Where(NotStudentAccount);
            }

            // ⚠️ متغيّر محلّي لا DateTimeOffset.UtcNow جوّه الـ Select:
            //    كده الوقت بيتبعت كـ parameter لـ SQL بدل ما EF تحاول
            //    تترجمه، والصفوف كلها بتتقارن بنفس اللحظة بالظبط.
            var now = DateTimeOffset.UtcNow;

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
                    // ⚠️ القفل ده بتاع Identity (LockoutEnd) لا is_active بتاعنا.
                    //    الشاشة محتاجة تفرّق بينهم عشان المسؤول يبطّل يعطّل
                    //    ويفعّل حساب مقفول ويستغرب إنه لسه مش بيدخل.
                    is_locked = u.LockoutEnd != null && u.LockoutEnd > now,
                    lockout_end = u.LockoutEnd,
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
                    ? "الحساب موجود ضمن المحذوفين - استعِده من تبويب «المحذوفون» بدل إنشائه من جديد"
                    : "اسم المستخدم مستخدم بالفعل", 409);

            // ⚠️ الحراسة قبل الإنشاء لا بعده: لو اترفض منح الدور بعد ما الحساب
            //    اتعمل، هنبقى سبنا مستخدمًا بلا دور في القاعدة.
            var newUserRole = string.IsNullOrWhiteSpace(dto.role) ? DefaultRoleName : dto.role.Trim().ToLowerInvariant();
            await GuardRoleGrantAsync(newUserRole, null, 0);

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

            await AssignRoleInternalAsync(user, newUserRole);

            await AddAuditAsync("user_created", user.Id);
            await UnitOfWork.SaveAsync();

            return await GetUserDetailAsync(user.Id);
        }

        public async Task<UserDetailDto> UpdateAsync(int id, UserUpdateDto dto)
        {
            var user = await _userManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            // ================= الحراسة قبل أي كتابة =================
            // ⚠️ الترتيب مقصود: لو الطلب هيترفض، ما ينفعش يكون الاسم والقسم
            //    اتغيّروا فعلًا والدور بس هو اللي اترفض - فنبقى سبنا الصف نُصّه
            //    متعدّل. الفحص كله بيتم قبل أول UpdateAsync.
            var newRole = string.IsNullOrWhiteSpace(dto.role) ? null : dto.role.Trim().ToLowerInvariant();
            // ⚠️ بنمسك القيمة نفسها لا علامة bool: المحلّل مش بيقدر يتتبّع فحص
            //    الـ null عبر متغيّر منطقي، فكان بيطلّع تحذير CS8604 على
            //    ResetPasswordAsync. المتغيّر ده بيحلّها من غير معامل قمع (!)
            //    - والقمع كان هيخفي التحذير من غير ما يضمن الشرط فعلًا.
            var newPassword = string.IsNullOrWhiteSpace(dto.password) ? null : dto.password;

            string? currentRole = null;
            var roleChanging = false;

            if (newRole != null || newPassword != null)
            {
                currentRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
                roleChanging = newRole != null
                    && !string.Equals(newRole, currentRole, StringComparison.OrdinalIgnoreCase);

                if (roleChanging || newPassword != null)
                    await GuardPrivilegedTargetAsync(user);

                if (roleChanging)
                    await GuardRoleGrantAsync(newRole!, currentRole, user.Id);
            }
            // ========================================================

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

            // ⚠️ بنكتب الدور لما يتغيّر بس. قبل كده كان بيُعاد إسناده مع كل حفظ
            //    (إزالة ثم إضافة) حتى لو هو نفسه - كتابتان بلا داعٍ في كل تعديل
            //    اسم، وسطر تدقيق مضلّل.
            if (roleChanging)
                await AssignRoleInternalAsync(user, newRole!);

            if (newPassword != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var pwdRes = await _userManager.ResetPasswordAsync(user, token, newPassword);
                if (!pwdRes.Succeeded)
                    throw new UserFriendlyException("تعذّر تعيين كلمة المرور: " + IdentityErrors(pwdRes), 400);
            }

            await AddAuditAsync("user_updated", user.Id);
            await UnitOfWork.SaveAsync();

            // ⚠️ حالة الحساب وقسمه متخزّنين في الـ claims بكاش دقيقة
            //    (PermissionClaimsTransformation). من غير الإبطال ده المسؤول يغيّر
            //    قسم المشرفة ويقولها جرّبي، فتلاقي نفس الشاشة القديمة وتفتكر إن
            //    التعديل ماحصلش. والتعديل هنا بيغيّر is_active كمان (dto.is_active).
            PermissionClaimsTransformation.InvalidateUser(_cache, user.Id);

            return await GetUserDetailAsync(user.Id);
        }

        public async Task SetActiveAsync(int id, bool active)
        {
            var user = await _userManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            // الحساب المحذوف بيتستعاد الأول، وبعدها يتفعّل — قرارين منفصلين عن قصد
            if (user.is_deleted)
                throw new UserFriendlyException("الحساب محذوف - استعِده أولًا من تبويب «المحذوفون»", 400);

            user.is_active = active;
            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر تحديث حالة المستخدم: " + IdentityErrors(res), 400);

            await AddAuditAsync(active ? "user_activated" : "user_deactivated", user.Id);
            await UnitOfWork.SaveAsync();

            // ⚠️ الإبطال ده هو اللي بيخلّي «إيقاف» يعني إيقاف. من غيره الموظف
            //    الموقوف يفضل شغّال بكامل صلاحياته لحد ما الكاش ينتهي — والأهم
            //    إن الفحص نفسه (PermissionClaimsTransformation) بيقرا من الكاش،
            //    فبدونه الإيقاف بياخد لحد دقيقة يسري. الحالة دي بالذات (نهاية
            //    تعاقد، نقل، حادثة أمنية) مالهاش دقيقة تستنّاها.
            PermissionClaimsTransformation.InvalidateUser(_cache, user.Id);
        }

        // ============================================================================
        //  فكّ قفل الحساب بعد محاولات الدخول الفاشلة.
        //
        //  ⚠️ ليه ده إجراء منفصل عن «تفعيل»: القفل بيتكتب في عمودين من عند
        //     Identity (LockoutEnd و AccessFailedCount)، والتفعيل بيكتب في
        //     عمود is_active بتاعنا. المسؤول كان بيعطّل الحساب ويفعّله تاني
        //     وبيستغرب إن المستخدم لسه مش قادر يدخل - لأن الحاجتين مالهمش
        //     علاقة ببعض خالص.
        //
        //  ⚠️ وبنصفّر AccessFailedCount مع LockoutEnd: لو صفّرنا التاريخ بس،
        //     العدّاد بيفضل على ٣ فأول محاولة فاشلة جاية بتقفل الحساب من
        //     تاني على طول - والمستخدم يفتكر إن الفكّ ما اشتغلش.
        //
        //  ⚠️ ومفيش InvalidateUser هنا: القفل بيتفحص وقت تسجيل الدخول
        //     (Services/AuthService)، مش من كاش الصلاحيات. الحساب المقفول
        //     مالوش جلسة شغّالة يتبطّل كاشها أصلًا.
        // ============================================================================
        public async Task UnlockAsync(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            var res = await _userManager.SetLockoutEndDateAsync(user, null);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر فكّ قفل الحساب: " + IdentityErrors(res), 400);

            res = await _userManager.ResetAccessFailedCountAsync(user);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر تصفير عدّاد المحاولات: " + IdentityErrors(res), 400);

            await AddAuditAsync("user_unlocked", user.Id);
            await UnitOfWork.SaveAsync();
        }

        public async Task AssignRoleAsync(int id, string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new UserFriendlyException("الدور مطلوب", 400);

            var user = await _userManager.FindByIdAsync(id.ToString())
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            // ⚠️ نفس حرّاس UpdateAsync: النقطة دي محميّة بـ roles.assign أصلًا،
            //    لكن الصلاحية وحدها ما بتمنعش صاحبها من ترقية نفسه ولا من منح
            //    دور مدير النظام - والحارس هو اللي بيمنع الاتنين.
            var normalized = role.Trim().ToLowerInvariant();
            var currentRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

            if (!string.Equals(normalized, currentRole, StringComparison.OrdinalIgnoreCase))
            {
                await GuardPrivilegedTargetAsync(user);
                await GuardRoleGrantAsync(normalized, currentRole, user.Id);
            }

            await AssignRoleInternalAsync(user, normalized);
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

            // الحذف زي الإيقاف بالظبط: لازم يقطع الجلسة القائمة فورًا لا بعد دقيقة
            PermissionClaimsTransformation.InvalidateUser(_cache, user.Id);
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

            // ⚠️ الاستعادة كمان: الحساب بيرجع معطّلًا، والكاش لو فضل شايله بحالته
            //    القديمة يبقى فيه فرق بين اللي في الشاشة واللي بيتطبّق فعلًا.
            PermissionClaimsTransformation.InvalidateUser(_cache, user.Id);
        }

        // ----------------------------- LDAP -----------------------------

        public async Task<List<LdapUserSearchItemDto>> SearchLdapAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                throw new UserFriendlyException("يرجى إدخال حرفين على الأقل للبحث", 400);

            var res = await _adService.SearchUsersAsync(query.Trim(), 25);
            if (!res.Success)
                throw new UserFriendlyException(res.Error ?? "تعذّر الاتصال بالـAD", 400);

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
                    ? "الحساب موجود ضمن المحذوفين - استعِده من تبويب «المحذوفون» بدل إضافته من جديد"
                    : "المستخدم موجود بالفعل في النظام", 409);

            // ⚠️ نفس الحارس: النقطة دي محميّة بـ users.addFromLdap، وهي لا تعني
            //    إسناد الأدوار. من غير السطر ده كانت بتقبل role:"admin" مباشرة.
            var ldapUserRole = string.IsNullOrWhiteSpace(dto.role) ? DefaultRoleName : dto.role.Trim().ToLowerInvariant();
            await GuardRoleGrantAsync(ldapUserRole, null, 0);

            var ad = await _adService.GetUserBySamAccountNameAsync(sam);
            if (!ad.Success)
                throw new UserFriendlyException(ad.Error ?? "تعذّر إيجاد المستخدم في الـAD", 400);

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
                throw new UserFriendlyException("تعذّر إضافة المستخدم من الـAD: " + IdentityErrors(res), 400);

            await AssignRoleInternalAsync(user, ldapUserRole);

            await AddAuditAsync("user_added_from_ldap", user.Id);
            await UnitOfWork.SaveAsync();

            return await GetUserDetailAsync(user.Id);
        }

        // ----------------------------- Helpers -----------------------------

        // ====================================================================
        //  حرّاس ترقية الصلاحيات
        //
        //  ⚠️ الثغرة اللي بيقفلها الكود ده: PUT /api/Users/{id} كان محميًّا
        //     بـ users.manage وحدها - وهي صلاحية إدارية عادية - وUpdateAsync
        //     كان بيطبّق منها **الدور** و**كلمة المرور** بلا أي فحص إضافي.
        //     فموظف معه users.manage بس كان يقدر:
        //       (أ) ينادي الـ PUT على حسابه هو بـ role:"admin" فيبقى مدير نظام،
        //       (ب) أو يضبط كلمة مرور أي حساب مدير ويدخل بيه.
        //     مع إن تغيير الدور له صلاحية منفصلة (roles.assign) على
        //     POST /{id}/role - فالـ PUT كان بيتخطّاها من غير ما حد ياخد باله.
        //
        //  الحرّاس دول بيتنادوا من **كل** مسار بيمنح دورًا أو يضبط كلمة مرور
        //  (Update و AssignRole و Create و AddFromLdap) عشان ما يفضلش باب
        //  خلفي في واحد منهم.
        // ====================================================================

        // الدور الافتراضي للحساب الجديد (قراءة فقط) - إنشاؤه جزء من users.manage
        private const string DefaultRoleName = "user";

        // من الـ claims زي أي سياسة - PermissionClaimsTransformation بتحمّلها كل طلب
        private bool ActorCanAssignRoles =>
            _http.HttpContext?.User?.HasClaim(ClaimConstants.Permission, "roles.assign") == true;

        // ⚠️ من القاعدة لا من التوكن: دور الفاعل ممكن يكون اتغيّر بعد ما دخل،
        //    والقرار ده أخطر من إنه يعتمد على نسخة قديمة محفوظة في الكوكي.
        private async Task<bool> ActorIsAdminAsync()
        {
            var actorId = UnitOfWork.GetCurrentUserId();
            if (actorId <= 0) return false;
            var actor = await _userManager.FindByIdAsync(actorId.ToString());
            return actor != null && await _userManager.IsInRoleAsync(actor, AdminRoleName);
        }

        // حساب مدير النظام لا يُمسّ دوره ولا كلمة مروره إلا من مدير نظام آخر
        private async Task GuardPrivilegedTargetAsync(User target)
        {
            var targetRoles = await _userManager.GetRolesAsync(target);
            var targetIsAdmin = targetRoles.Any(r => string.Equals(r, AdminRoleName, StringComparison.OrdinalIgnoreCase));

            if (targetIsAdmin && !await ActorIsAdminAsync())
                throw new UserFriendlyException(
                    "تعديل دور حساب مدير النظام أو كلمة مروره لا يتم إلا من مدير نظام", 403);
        }

        // حارس منح الدور. currentRole = الدور الحالي (null للحساب الجديد)،
        // targetUserId = 0 للحساب الجديد لأنه لا يمكن أن يكون الفاعل نفسه.
        private async Task GuardRoleGrantAsync(string requestedRole, string? currentRole, int targetUserId)
        {
            // ⚠️ نفس الدور ⇒ مافيش قرار امتيازات أصلًا. الشرط ده ضروري لأن شاشة
            //    المستخدمين بتبعت الدور في **كل** حفظ، فبدونه أي تعديل لاسم أو
            //    جوال كان هيتطلب roles.assign ويكسر الاستعمال العادي لـ users.manage.
            if (string.Equals(requestedRole, currentRole, StringComparison.OrdinalIgnoreCase))
                return;

            // حساب جديد بالدور الافتراضي: ده «إضافة موظف» وهي ضمن users.manage
            var isNewWithDefaultRole = currentRole == null
                && string.Equals(requestedRole, DefaultRoleName, StringComparison.OrdinalIgnoreCase);

            if (!isNewWithDefaultRole && !ActorCanAssignRoles)
                throw new UserFriendlyException(
                    "تغيير دور المستخدم يحتاج صلاحية «إسناد الأدوار»", 403);

            // ⚠️ ولا حتى من معه roles.assign يرفّع نفسه: الترقية لازم تيجي من
            //    شخص تاني، وإلا الصلاحية دي وحدها بتساوي مدير نظام.
            var actorId = UnitOfWork.GetCurrentUserId();
            if (targetUserId > 0 && actorId > 0 && actorId == targetUserId)
                throw new UserFriendlyException("لا يمكنك تغيير دور حسابك الشخصي", 403);

            // منح أعلى دور في النظام لا يكون إلا ممن يملكه
            if (string.Equals(requestedRole, AdminRoleName, StringComparison.OrdinalIgnoreCase)
                && !await ActorIsAdminAsync())
                throw new UserFriendlyException("منح دور مدير النظام لا يتم إلا من مدير نظام", 403);
        }

        private async Task AssignRoleInternalAsync(User user, string role)
        {
            // ⚠️ كان بينشئ الدور لو مش موجود، فقيمة نصية جاية من العميل كانت
            //    بتصنع أدوارًا جديدة في قاعدة البيانات - دور بلا صلاحيات ومحدش
            //    طالبه. الأدوار تُنشأ من شاشة «الأدوار والصلاحيات» وحدها.
            if (!await _roleManager.RoleExistsAsync(role))
                throw new UserFriendlyException($"الدور «{role}» غير موجود", 400);

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
