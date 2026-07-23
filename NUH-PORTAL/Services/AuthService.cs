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
        private readonly RoleManager<Role> _roleManager;
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
            RoleManager<Role> roleManager,
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
            _roleManager = roleManager;
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

        public async Task<User> AuthenticateAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new UserFriendlyException("اسم المستخدم وكلمة المرور مطلوبان", 400);

            var (clientIp, _) = ClientInfo();
            var adResult = await _adService.AuthenticateAsync(username, password, clientIp);

            bool loginFailedLogged = false;

            if (adResult.IsAdAvailable)
            {
                if (adResult.IsAuthenticated && adResult.Details != null)
                {
                    var (user, isNew) = await SyncAdUserAsync(adResult);

                    await AddAuditLogAsync(user.Id, isNew ? "user_created_ad" : "user_updated_ad", "Users", user.Id);
                    await AddAuditLogAsync(user.Id, "login", "Users", user.Id);
                    await AddSignInLogAsync(user.Id, user.UserName, "login_success", "ad", true, null);
                    await UnitOfWork.SaveAsync();

                    _logger.LogInformation("AD login succeeded for {Username}, IP: {ClientIp}", username, clientIp ?? "unknown");
                    return user;
                }

                _logger.LogWarning("AD login failed for {Username}, IP: {ClientIp}", username, clientIp ?? "unknown");
                await AddAuditLogAsync(0, "login_failed", "Users", 0);
                loginFailedLogged = true;
            }
            else
            {
                _logger.LogInformation("AD unavailable, checking local fallback for {Username}, IP: {ClientIp}", username, clientIp ?? "unknown");
            }

            var localUser = await _userManager.FindByNameAsync(username);
            if (localUser != null && localUser.is_active && await _userManager.CheckPasswordAsync(localUser, password))
            {
                await AddAuditLogAsync(localUser.Id, "login_local_fallback", "Users", localUser.Id);
                await AddSignInLogAsync(localUser.Id, localUser.UserName, "login_success", "local", true, null);
                await UnitOfWork.SaveAsync();
                _logger.LogInformation("Local fallback login succeeded for {Username}, IP: {ClientIp}", username, clientIp ?? "unknown");
                return localUser;
            }

            if (!loginFailedLogged)
                await AddAuditLogAsync(localUser?.Id ?? 0, "login_failed", "Users", localUser?.Id ?? 0);
            await AddAuditLogAsync(0, "login_local_fallback_failed", "Users", 0);
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
            var (ip, ua) = ClientInfo();
            await _auditLogs.AddAsync(new AuditLog
            {
                user_id = userId,
                action = "logout",
                target_table = "Users",
                target_id = userId,
                action_at = DateTime.UtcNow,
                ip_address = ip,
                user_agent = ua
            });
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

        private async Task EnsureRoleExistsAsync(string role)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new Role(role));
        }

        private async Task<(User user, bool isNew)> SyncAdUserAsync(AdAuthResult adResult)
        {
            var details = adResult.Details!;
            var role = string.IsNullOrWhiteSpace(adResult.Role) ? "user" : adResult.Role.ToLowerInvariant();
            await EnsureRoleExistsAsync(role);

            var user = await _userManager.FindByNameAsync(details.Username);
            bool isNew = user == null;

            if (isNew)
            {
                user = new User
                {
                    UserName = details.Username,
                    Email = details.Email,
                    full_name = details.DisplayName,
                    is_active = true,
                    created_at = DateTime.UtcNow,
                    department = details.Department,
                    mobile = details.Mobile,
                    job_title = details.JobTitle
                };
                // مستخدم AD من غير باسورد محلي (الدخول عبر AD)
                var createRes = await _userManager.CreateAsync(user);
                if (!createRes.Succeeded)
                    throw new UserFriendlyException("فشل إنشاء المستخدم من AD: " + string.Join(", ", createRes.Errors.Select(e => e.Description)), 500);
                _logger.LogInformation("Created new user from AD: {Username}, role: {Role}", details.Username, role);
            }
            else
            {
                user.full_name = details.DisplayName;
                user.Email = details.Email;
                user.is_active = true;
                user.department = details.Department;
                user.mobile = details.Mobile;
                user.job_title = details.JobTitle;
                await _userManager.UpdateAsync(user);
                _logger.LogInformation("Updated user from AD: {Username}, role: {Role}", details.Username, role);
            }

            // ضمان الدور (single-role حاليًا: نشيل القديم ونحط دور الـ AD)
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(role))
            {
                if (currentRoles.Count > 0) await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, role);
            }

            return (user, isNew);
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
