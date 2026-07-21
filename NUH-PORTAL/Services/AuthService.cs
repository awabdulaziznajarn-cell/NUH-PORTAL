using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Auth;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // منطق المصادقة — اتنقل من AuthController بنفس التدفق بالظبط:
    // AD أولًا (لو متاح) → مزامنة المستخدم → توكن. لو AD فشل/مش متاح → fallback محلي بـ BCrypt.
    public class AuthService : AppServiceBase, IAuthService
    {
        private readonly IRepository<User> _users;
        private readonly IRepository<AuditLog> _auditLogs;
        private readonly ActiveDirectoryService _adService;
        private readonly ITokenService _tokens;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IRepository<User> users,
            IRepository<AuditLog> auditLogs,
            ActiveDirectoryService adService,
            ITokenService tokens,
            IMemoryCache cache,
            IHttpContextAccessor http,
            ILogger<AuthService> logger,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _users = users;
            _auditLogs = auditLogs;
            _adService = adService;
            _tokens = tokens;
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
            var token = _tokens.GenerateToken(user);
            return BuildResult(user, token);
        }

        public async Task<User> AuthenticateAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new UserFriendlyException("اسم المستخدم وكلمة المرور مطلوبان", 400);
            var request = new LoginRequest { username = username, password = password };

            var (clientIp, _) = ClientInfo();
            var adResult = await _adService.AuthenticateAsync(request.username, request.password, clientIp);

            bool loginFailedLogged = false;

            if (adResult.IsAdAvailable)
            {
                if (adResult.IsAuthenticated && adResult.Details != null)
                {
                    var (user, isNew) = await SyncAdUserAsync(adResult);

                    try
                    {
                        await UnitOfWork.SaveAsync();
                    }
                    catch (DbUpdateException ex)
                    {
                        _logger.LogCritical(ex,
                            "DB save failed during AD user sync for {Username} (AD role={Role}): {Inner}",
                            adResult.Details?.Username, adResult.Role, ex.InnerException?.Message ?? ex.Message);
                        throw;
                    }

                    await AddAuditLogAsync(user.Id, isNew ? "user_created_ad" : "user_updated_ad", "Users", user.Id);
                    await AddAuditLogAsync(user.Id, "login", "Users", user.Id);
                    await UnitOfWork.SaveAsync();

                    _logger.LogInformation("AD login succeeded for {Username}, role: {Role}, IP: {ClientIp}",
                        request.username, user.role, clientIp ?? "unknown");

                    return user;
                }

                _logger.LogWarning("AD login failed for {Username}, IP: {ClientIp}", request.username, clientIp ?? "unknown");
                await AddAuditLogAsync(0, "login_failed", "Users", 0);
                loginFailedLogged = true;
            }
            else
            {
                _logger.LogInformation("AD unavailable, checking local admin fallback for {Username}, IP: {ClientIp}",
                    request.username, clientIp ?? "unknown");
            }

            var localUser = await _users.FindAsync(u => u.username == request.username
                && u.is_active == true
                && !string.IsNullOrEmpty(u.password_hash));

            if (localUser != null && BCrypt.Net.BCrypt.Verify(request.password, localUser.password_hash))
            {
                await AddAuditLogAsync(localUser.Id, "login_local_fallback", "Users", localUser.Id);
                await UnitOfWork.SaveAsync();

                _logger.LogInformation("Local fallback login succeeded for {Username} (role={Role}), IP: {ClientIp}",
                    request.username, localUser.role, clientIp ?? "unknown");

                return localUser;
            }

            if (!loginFailedLogged)
                await AddAuditLogAsync(localUser?.Id ?? 0, "login_failed", "Users", localUser?.Id ?? 0);
            await AddAuditLogAsync(0, "login_local_fallback_failed", "Users", 0);
            await UnitOfWork.SaveAsync();

            _logger.LogWarning("Local fallback login failed for {Username}, IP: {ClientIp}",
                request.username, clientIp ?? "unknown");

            throw new UserFriendlyException("اسم المستخدم أو كلمة المرور غير صحيحة", 401);
        }

        public async Task SetPasswordAsync(SetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.username) || string.IsNullOrWhiteSpace(request.password))
                throw new UserFriendlyException("اسم المستخدم وكلمة المرور مطلوبان", 400);

            var user = await _users.FindAsync(u => u.username == request.username)
                ?? throw UserFriendlyException.NotFound("المستخدم غير موجود");

            user.password_hash = BCrypt.Net.BCrypt.HashPassword(request.password);
            await UnitOfWork.SaveAsync();

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

            // (زي الكود القديم: بيتسجل حتى لو userId = 0)
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
            await UnitOfWork.SaveAsync();
        }

        // ----------------------------- Helpers -----------------------------

        private async Task<(User user, bool isNew)> SyncAdUserAsync(AdAuthResult adResult)
        {
            var details = adResult.Details!;
            var user = await _users.FindAsync(u => u.username == details.Username);

            if (user == null)
            {
                user = new User
                {
                    username = details.Username,
                    full_name = details.DisplayName,
                    email = details.Email,
                    role = (adResult.Role ?? "").ToLowerInvariant(),
                    is_active = true,
                    created_at = DateTime.UtcNow,
                    department = details.Department,
                    mobile = details.Mobile,
                    job_title = details.JobTitle,
                    password_hash = null
                };
                await _users.AddAsync(user);
                _logger.LogInformation("Created new user from AD: {Username}, role: {Role}", details.Username, adResult.Role);
                return (user, true);
            }

            user.full_name = details.DisplayName;
            user.email = details.Email;
            user.role = (adResult.Role ?? "").ToLowerInvariant();
            user.is_active = true;
            user.department = details.Department;
            user.mobile = details.Mobile;
            user.job_title = details.JobTitle;
            _logger.LogInformation("Updated user from AD: {Username}, role: {Role}", details.Username, adResult.Role);

            return (user, false);
        }

        // نفس منطق الكنترولر القديم: مفيش تسجيل لو مفيش مستخدم معروف
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

        private static LoginResultDto BuildResult(User user, string token) => new()
        {
            Message = "تم تسجيل الدخول بنجاح",
            Token = token,
            User = new AuthUserDto
            {
                id = user.Id,
                username = user.username,
                full_name = user.full_name,
                role = user.role
            }
        };
    }
}
