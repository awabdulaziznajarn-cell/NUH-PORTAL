using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Auth;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // منطق المصادقة — AD أولًا (لو متاح) → مزامنة المستخدم عبر UserManager → توكن.
    // لو AD فشل/مش متاح → fallback محلي عبر UserManager.CheckPasswordAsync (هاشر Identity).
    public class AuthService : AppServiceBase, IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly IRepository<AuditLog> _auditLogs;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ActiveDirectoryService _adService;
        private readonly ITokenService _tokens;
        private readonly IPermissionService _permissions;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<User> userManager,
            IRepository<AuditLog> auditLogs,
            IServiceScopeFactory scopeFactory,
            ActiveDirectoryService adService,
            ITokenService tokens,
            IPermissionService permissions,
            IMemoryCache cache,
            IHttpContextAccessor http,
            ILogger<AuthService> logger,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _userManager = userManager;
            _auditLogs = auditLogs;
            _scopeFactory = scopeFactory;
            _adService = adService;
            _tokens = tokens;
            _permissions = permissions;
            _cache = cache;
            _http = http;
            _logger = logger;
        }

        private (string? ip, string ua) ClientInfo()
        {
            var ctx = _http.HttpContext;
            return (ctx?.Connection.RemoteIpAddress?.ToString(), ctx?.Request.Headers["User-Agent"].ToString() ?? "");
        }

        public async Task<LoginResultDto> LoginAsync(LoginRequest request)
        {
            var user = await AuthenticateAsync(request.username, request.password);
            var roles = await _userManager.GetRolesAsync(user);
            var perms = await _permissions.GetPermissionsForRolesAsync(roles);
            var token = _tokens.GenerateToken(user, roles, perms);
            return BuildResult(user, roles, token);
        }

        // ====================================================================
        //  ⚠️ أحداث الحسابات (دخول، فشل، قفل، خروج) بتتسجّل في SignInLogs وبس.
        //
        //  كانت بتتكتب في الجدولين: SignInLogs بالتفصيل الكامل، و AuditLogs
        //  بأسماء زي login و login_failed و login_local_fallback و logout
        //  و user_updated_ad. والنسخة اللي في AuditLogs أفقر — مافيهاش طريقة
        //  الدخول ولا النتيجة ولا الـ IP، يعني الحاجات اللي بتفيد في التحقيق.
        //  فكانت بتزحم شاشة «سجل العمليات» بضجيج والمعلومة الكاملة في الشاشة
        //  المخصصة ليها. و user_updated_ad تحديدًا كانت بتتكتب مع *كل* تسجيل
        //  دخول من غير ما تقول اتغيّر إيه.
        //
        //  القسمة دلوقتي: سجل العمليات للتغييرات على البيانات، وسجل الدخول
        //  والخروج للحسابات. كل معلومة في مكان واحد.
        //  (set_password فاضل في سجل العمليات — ده تغيير بيانات مش حدث دخول.)
        // ====================================================================
        public async Task<User> AuthenticateAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new UserFriendlyException("اسم المستخدم وكلمة المرور مطلوبان", 400);

            var (clientIp, _) = ClientInfo();

            // ====================================================================
            //  ⚠️ قفل الحساب — الفحص قبل أي محاولة مصادقة.
            //
            //     ماكانش في أي قفل: عدد المحاولات مفتوح على الآخر، ومسار
            //     المصادقة بيستخدم CheckPasswordAsync اللي **لا** بتزوّد عدّاد
            //     الفشل ولا بتفحص القفل. فالتخمين كان بلا سقف.
            //
            //     بنفحص هنا قبل ما نبعت أي شيء للأكتف دايركتوري كمان — عشان
            //     محاولات التخمين ما تستهلكش سياسة القفل بتاعة الدومين وتقفل
            //     حساب الموظف في الشبكة كلها لا في نظامنا وحده.
            // ====================================================================
            var lockedUser = await _userManager.FindByNameAsync(username);
            if (lockedUser != null && await _userManager.IsLockedOutAsync(lockedUser))
            {
                await AddSignInLogAsync(lockedUser.Id, username, "login_failed",
                    "lockout", false, "الحساب مقفول مؤقتًا بعد محاولات فاشلة متكررة");
                await UnitOfWork.SaveAsync();

                _logger.LogWarning("Login blocked - account locked out: {Username}, IP: {ClientIp}",
                    username, clientIp ?? "unknown");

                throw new UserFriendlyException(
                    "تم قفل الحساب مؤقتًا بعد عدة محاولات فاشلة. حاول بعد ١٥ دقيقة.", 423);
            }

            var adResult = await _adService.AuthenticateAsync(username, password, clientIp);

            if (adResult.IsAdAvailable)
            {
                if (adResult.IsAuthenticated && adResult.Details != null)
                {
                    // ⚠️ الدومين أكّد إن الباسورد صح — وده لوحده مش كفاية.
                    //    الحساب لازم يكون متضاف عندنا من شاشة «إدارة المستخدمين»
                    //    (زرار «إضافة من الدليل»). قبل كده أي موظف على الدومين يعرف
                    //    الرابط كان يدخل والنظام يعمله حساب تلقائي من غير موافقة حد،
                    //    والصف يتحسب في تبويب «حسابات دخول الطلاب» مخلوط بحسابات
                    //    الطلاب الحقيقية. مين له حساب قرار من الشاشة، زيه زي الدور.
                    var known = await _userManager.FindByNameAsync(adResult.Details.Username);
                    if (known == null)
                    {
                        await AddSignInLogAsync(null, adResult.Details.Username, "login_failed", "ad", false, "غير مسجّل في النظام");
                        await UnitOfWork.SaveAsync();
                        _logger.LogWarning("AD login rejected - account not provisioned: {Username}, IP: {ClientIp}", username, clientIp ?? "unknown");
                        throw new UserFriendlyException("حسابك غير مسجّل في نظام إسكان الطلاب. راجع مدير النظام لإضافة حسابك.", 403);
                    }

                    // ⚠️ حساب متوقّف أو محذوف مايدخلش — حتى لو الدومين قال إن الباسورد صح.
                    //    مسار الدخول المحلي تحت بيفحص is_active، ومسار الـ AD ماكانش
                    //    بيفحصه خالص. يعني زرار «تعطيل» في شاشة المستخدمين مكانش
                    //    بيعمل أي حاجة لأي موظف بيدخل عبر الدومين — وهم كل الموظفين.
                    if (known.is_deleted || !known.is_active)
                    {
                        var reason = known.is_deleted ? "الحساب محذوف" : "الحساب متوقّف";
                        await AddSignInLogAsync(known.Id, known.UserName, "login_failed", "ad", false, reason);
                        await UnitOfWork.SaveAsync();
                        _logger.LogWarning("Login blocked ({Reason}): {Username}, IP: {ClientIp}", reason, username, clientIp ?? "unknown");
                        // ⚠️ نفس الرسالة للحالتين: المستخدم مايعرفش من الرسالة إن الحساب
                        //    اتحذف بالذات ولا اتعطّل — السبب الدقيق في السجل للمسؤول بس.
                        throw new UserFriendlyException("هذا الحساب غير مفعّل. راجع مدير النظام.", 403);
                    }

                    var user = await SyncAdUserAsync(adResult);

                    // دخول ناجح يمسح تاريخ الفشل — وإلا محاولات متفرقة على مدى
                    // أيام بتتجمّع وتقفل حساب موظف شغّال عادي.
                    await _userManager.ResetAccessFailedCountAsync(user);

                    await AddSignInLogAsync(user.Id, user.UserName, "login_success", "ad", true, null);
                    await UnitOfWork.SaveAsync();

                    _logger.LogInformation("AD login succeeded for {Username}, IP: {ClientIp}", username, clientIp ?? "unknown");
                    return user;
                }

                _logger.LogWarning("AD login failed for {Username}, IP: {ClientIp}", username, clientIp ?? "unknown");
            }
            else
            {
                _logger.LogInformation("AD unavailable, checking local fallback for {Username}, IP: {ClientIp}", username, clientIp ?? "unknown");
            }

            var localUser = lockedUser;
            // المحذوف بيتعامل زي بيانات غلط على المسار المحلي — مافيش تفرقة تفيد مخمّن
            if (localUser != null && !localUser.is_deleted && localUser.is_active && await _userManager.CheckPasswordAsync(localUser, password))
            {
                await _userManager.ResetAccessFailedCountAsync(localUser);
                await AddSignInLogAsync(localUser.Id, localUser.UserName, "login_success", "local", true, null);
                await UnitOfWork.SaveAsync();
                _logger.LogInformation("Local fallback login succeeded for {Username}, IP: {ClientIp}", username, clientIp ?? "unknown");
                return localUser;
            }

            // ⚠️ ده السطر اللي كان ناقص: من غيره عدّاد الفشل يفضل صفر للأبد
            //    والقفل ما بيحصلش مهما كان عدد المحاولات.
            //
            // ⚠️ والسطر اللي قبله ضروري كمان: Identity بيقفل الحساب بس لو
            //    LockoutEnabled = true عليه. الإعداد في Program.cs بيطبّق على
            //    المستخدمين الجدد وقت الإنشاء بس — أما الصفوف الموجودة من قبل
            //    فقيمتها متخزّنة في قاعدة البيانات زي ما هي. من غير التفعيل ده
            //    العدّاد بيزيد والقفل ما بيجيش أبدًا للمستخدمين الحاليين.
            if (localUser != null)
            {
                if (!await _userManager.GetLockoutEnabledAsync(localUser))
                    await _userManager.SetLockoutEnabledAsync(localUser, true);

                await _userManager.AccessFailedAsync(localUser);
            }

            await AddSignInLogAsync(localUser?.Id, username, "login_failed", adResult.IsAdAvailable ? "ad" : "local", false, "بيانات الدخول غير صحيحة");
            await UnitOfWork.SaveAsync();

            _logger.LogWarning("Local fallback login failed for {Username}, IP: {ClientIp}", username, clientIp ?? "unknown");
            throw new UserFriendlyException("اسم المستخدم أو كلمة المرور غير صحيحة", 401);
        }

        public async Task SetPasswordAsync(SetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.username) || string.IsNullOrWhiteSpace(request.password))
                throw new UserFriendlyException("اسم المستخدم وكلمة المرور مطلوبان", 400);

            var user = await _userManager.FindByNameAsync(request.username)
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var res = await _userManager.ResetPasswordAsync(user, resetToken, request.password);
            if (!res.Succeeded)
                throw new UserFriendlyException("تعذّر تعيين كلمة المرور: " + string.Join(", ", res.Errors.Select(e => e.Description)), 400);

            await AddAuditLogAsync(UnitOfWork.GetCurrentUserId(), "set_password", "Users", user.Id);
            await UnitOfWork.SaveAsync();
        }

        public void RecordActivity()
        {
            var userId = UnitOfWork.GetCurrentUserId();
            if (userId > 0)
                _cache.Set("activity_" + userId, DateTime.UtcNow, TimeSpan.FromMinutes(30));
        }

        public async Task LogoutAsync()
        {
            var userId = UnitOfWork.GetCurrentUserId();
            await AddSignInLogAsync(userId, _http.HttpContext?.User?.Identity?.Name, "logout", "session", true, null);
            await UnitOfWork.SaveAsync();
        }

        // ----------------------------- Helpers -----------------------------

        // تسجيل الدخول/الخروج best-effort على سياق منفصل — أي فشل (زي إن جدول SignInLogs
        // لسه ماتعملّوش migration) ميكسرش تسجيل الدخول/الخروج نفسه.
        private async Task AddSignInLogAsync(int? userId, string? username, string eventType, string method, bool success, string? detail)
        {
            try
            {
                var (ip, ua) = ClientInfo();
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Set<SignInLog>().Add(new SignInLog
                {
                    occurred_at = DateTime.UtcNow,
                    user_id = (userId.HasValue && userId.Value > 0) ? userId : null,
                    username = username,
                    event_type = eventType,
                    method = method,
                    success = success,
                    detail = detail,
                    ip_address = ip,
                    user_agent = ua
                });
                await db.SaveChangesAsync();
            }
            catch
            {
                // متعمّد: السجل best-effort — تسجيل الدخول/الخروج ما ينفعش يقع بسببه.
            }
        }

        // مزامنة بيانات الموظف من الدليل: الاسم والإيميل والقسم والجوال والمسمّى.
        //
        // ⚠️ مابتعملش حساب جديد ومابتلمسش الدور ولا is_active.
        //    • الإنشاء: الحساب لازم يكون متضاف من شاشة «إدارة المستخدمين».
        //    • الدور: مصدره الشاشة. كان بيتعاد حسابه من مجموعات الـ AD مع كل دخول
        //      وبيمسح اللي المسؤول حدّده، ولأن مجموعات Housing_* مش متعمولة على
        //      الدومين كانت النتيجة دايمًا "user" — فالموظف يختفي من الشاشة.
        //    • is_active: قرار إداري من الشاشة، مش حاجة الدليل بيقولها.
        //    الدليل مسؤوليته: يتأكد من الباسورد، ويجيب البيانات الشخصية. وبس.
        private async Task<User> SyncAdUserAsync(AdAuthResult adResult)
        {
            var details = adResult.Details!;

            var user = await _userManager.FindByNameAsync(details.Username)
                ?? throw new UserFriendlyException("حسابك غير مسجّل في نظام إسكان الطلاب. راجع مدير النظام لإضافة حسابك.", 403);

            user.full_name = details.DisplayName;
            user.Email = details.Email;
            user.department = details.Department;
            // ⚠️ AdUserDetails.Mobile قيمته الافتراضية "" مش null. والجوال عليه فهرس
            //    فريد مُصفّى بيستثني NULL بس، فالنص الفاضي بيدخله عادي — يعني تاني
            //    موظف دومين ملوش جوال في الدليل كان دخوله هيفشل بخطأ قاعدة بيانات،
            //    مش بخطأ مصادقة. فاضي = NULL.
            user.mobile = string.IsNullOrWhiteSpace(details.Mobile) ? null : details.Mobile.Trim();
            user.job_title = details.JobTitle;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Synced user from AD: {Username} (role and status kept as set in the portal)", details.Username);
            return user;
        }

        private async Task AddAuditLogAsync(int userId, string action, string targetTable, int targetId)
        {
            if (userId <= 0) return;
            var (ip, ua) = ClientInfo();
            await _auditLogs.AddAsync(new AuditLog
            {
                user_id = userId,
                action = action,
                target_table = targetTable,
                target_id = targetId,
                action_at = DateTime.UtcNow,
                ip_address = ip,
                user_agent = ua
            });
        }

        private static LoginResultDto BuildResult(User user, IList<string> roles, string token) => new()
        {
            Message = "تم تسجيل الدخول بنجاح",
            Token = token,
            User = new AuthUserDto
            {
                id = user.Id,
                username = user.UserName,
                full_name = user.full_name,
                role = roles.FirstOrDefault() ?? "user"
            }
        };
    }
}
