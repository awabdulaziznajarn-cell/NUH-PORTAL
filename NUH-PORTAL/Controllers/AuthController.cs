using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NUH_PORTAL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly ActiveDirectoryService _adService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AppDbContext context, IConfiguration config, ActiveDirectoryService adService, ILogger<AuthController> logger)
        {
            _context = context;
            _config = config;
            _adService = adService;
            _logger = logger;
        }

        [HttpPost("Login")]
        public async Task<ActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.username) || string.IsNullOrWhiteSpace(request.password))
            {
                return BadRequest(new { message = "اسم المستخدم وكلمة المرور مطلوبان" });
            }

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var adResult = await _adService.AuthenticateAsync(request.username, request.password, clientIp);

            _logger.LogInformation("TRACE: AuthController.Login decision — IsAdAvailable={Avail}, IsAuthenticated={Auth}, Role={Role}, HasDetails={Details}",
                adResult.IsAdAvailable, adResult.IsAuthenticated, adResult.Role, adResult.Details != null);

            bool loginFailedLogged = false;

            if (adResult.IsAdAvailable)
            {
                if (adResult.IsAuthenticated && adResult.Details != null)
                {
                    _logger.LogInformation("TRACE: Branch=AD_AUTH_OK — syncing user from AD");
                    var (user, isNew) = await SyncAdUser(adResult);

                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateException ex)
                    {
                        var inner = ex.InnerException;
                        var sqlNumber = 0;
                        try { sqlNumber = (int)(inner?.GetType().GetProperty("Number")?.GetValue(inner) ?? 0); } catch { }

                        var entityInfo = new System.Text.StringBuilder();
                        foreach (var entry in ex.Entries)
                        {
                            entityInfo.AppendLine($"  State={entry.State}, Entity={entry.Entity.GetType().Name}");
                            if (entry.Entity is User u)
                            {
                                entityInfo.AppendLine($"    username='{u.username}'");
                                entityInfo.AppendLine($"    role='{u.role}'");
                                entityInfo.AppendLine($"    email='{u.email}'");
                                entityInfo.AppendLine($"    full_name='{u.full_name}'");
                                entityInfo.AppendLine($"    department='{u.department}'");
                                entityInfo.AppendLine($"    mobile='{u.mobile}'");
                                entityInfo.AppendLine($"    job_title='{u.job_title}'");
                                entityInfo.AppendLine($"    is_active={u.is_active}");
                                entityInfo.AppendLine($"    created_at={u.created_at:O}");
                                entityInfo.AppendLine($"    password_hash='{(u.password_hash?.Length > 0 ? "(set)" : "(null)")}'");
                            }
                            else
                            {
                                foreach (var prop in entry.CurrentValues.Properties)
                                {
                                    var val = entry.CurrentValues[prop];
                                    entityInfo.AppendLine($"    {prop.Name}='{val}' (type={prop.ClrType.Name})");
                                }
                            }
                        }

                        var detailMsg = string.Format(
                            "DB SAVE FAILED: Err#{0} {1}{2}" +
                            "ExType={3}{2}" +
                            "InnerType={4}{2}" +
                            "Stack={5}{2}" +
                            "--- AD Context ---{2}" +
                            "  AD Role={6}, AD Username={7}{2}" +
                            "  AD DisplayName={8}, AD Email={9}{2}" +
                            "  AD Department={10}, AD Mobile={11}, AD JobTitle={12}{2}" +
                            "--- Entities ---{2}{13}",
                            sqlNumber,
                            inner?.Message ?? ex.Message,
                            System.Environment.NewLine,
                            ex.GetType().FullName,
                            inner?.GetType().FullName ?? "(null)",
                            ex.StackTrace,
                            adResult.Role,
                            adResult.Details?.Username ?? "(null)",
                            adResult.Details?.DisplayName ?? "(null)",
                            adResult.Details?.Email ?? "(null)",
                            adResult.Details?.Department ?? "(null)",
                            adResult.Details?.Mobile ?? "(null)",
                            adResult.Details?.JobTitle ?? "(null)",
                            entityInfo.ToString()
                        );
                        _logger.LogCritical(ex, detailMsg);
                        throw;
                    }

                    _logger.LogWarning(
                        "TRACE ROLE CHECK: AD Role={Role}, Username={Username}",
                        adResult.Role,
                        adResult.Details?.Username
                    );

                    var token = GenerateJwtToken(user);

                    if (isNew)
                        AddAuditLog(user.Id, "user_created_ad", "Users", user.Id);
                    else
                        AddAuditLog(user.Id, "user_updated_ad", "Users", user.Id);
                    AddAuditLog(user.Id, "login", "Users", user.Id);

                    _logger.LogInformation(
                        "AD login succeeded for {Username}, role: {Role}, IP: {ClientIp}",
                        request.username, user.role, clientIp ?? "unknown");

                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        message = "تم تسجيل الدخول بنجاح",
                        token,
                        user = new
                        {
                            id = user.Id,
                            username = user.username,
                            full_name = user.full_name,
                            role = user.role
                        }
                    });
                }

                _logger.LogInformation(
    "TRACE: AD authentication failed for {Username}, trying local admin fallback",
    request.username);

                _logger.LogWarning(
                    "AD login failed for {Username}, IP: {ClientIp}",
                    request.username, clientIp ?? "unknown");

                AddAuditLog(0, "login_failed", "Users", 0);
                loginFailedLogged = true;
            }

            if (!adResult.IsAdAvailable)
            {
                _logger.LogInformation(
                    "AD unavailable, checking local admin fallback for {Username}, IP: {ClientIp}",
                    request.username, clientIp ?? "unknown");
            }

            var localUser = await _context.Users
                .FirstOrDefaultAsync(u => u.username == request.username
                    && u.is_active == true
                    && !string.IsNullOrEmpty(u.password_hash));

            _logger.LogInformation("TRACE: Local fallback query result — user found: {Found}",
                localUser != null);

            if (localUser != null && BCrypt.Net.BCrypt.Verify(request.password, localUser.password_hash))
            {
                _logger.LogInformation("TRACE: Branch=LOCAL_FALLBACK_OK — password verified, returning token");

                var token = GenerateJwtToken(localUser);

                AddAuditLog(localUser.Id, "login_local_fallback", "Users", localUser.Id);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Local fallback login succeeded for {Username} (role={Role}), IP: {ClientIp}",
                    request.username, localUser.role, clientIp ?? "unknown");

                return Ok(new
                {
                    message = "تم تسجيل الدخول بنجاح",
                    token,
                    user = new
                    {
                        id = localUser.Id,
                        username = localUser.username,
                        full_name = localUser.full_name,
                        role = localUser.role
                    }
                });
            }

            _logger.LogInformation("TRACE: Branch=LOCAL_FALLBACK_FAILED — user not found or password wrong, returning 401");
            if (!loginFailedLogged)
                AddAuditLog(localUser?.Id ?? 0, "login_failed", "Users", localUser?.Id ?? 0);
            AddAuditLog(0, "login_local_fallback_failed", "Users", 0);
            await _context.SaveChangesAsync();

            _logger.LogWarning(
                "Local fallback login failed for {Username}, IP: {ClientIp}",
                request.username, clientIp ?? "unknown");
            return Unauthorized(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
        }

        [Authorize(Roles = "admin")]
        [HttpPost("SetPassword")]
        public async Task<ActionResult> SetPassword([FromBody] SetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.username) || string.IsNullOrWhiteSpace(request.password))
            {
                return BadRequest(new { message = "اسم المستخدم وكلمة المرور مطلوبان" });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.username == request.username);

            if (user == null)
                return NotFound(new { message = "المستخدم غير موجود" });

            user.password_hash = BCrypt.Net.BCrypt.HashPassword(request.password);
            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new AuditLog
            {
                user_id = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var actorId) ? actorId : 0,
                action = "set_password",
                target_table = "Users",
                target_id = user.Id,
                action_at = DateTime.UtcNow,
                ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                user_agent = Request.Headers["User-Agent"].ToString()
            });
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم ضبط كلمة المرور بنجاح" });
        }

        [Authorize]
        [HttpPost("Ping")]
        public ActionResult Ping()
        {
            var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;
            if (userId > 0)
            {
                var cache = HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                cache.Set("activity_" + userId, DateTime.UtcNow, TimeSpan.FromMinutes(30));
            }
            return Ok();
        }

        [Authorize]
        [HttpPost("Logout")]
        public async Task<ActionResult> Logout()
        {
            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

            _context.AuditLogs.Add(new AuditLog
            {
                user_id = userId,
                action = "logout",
                target_table = "Users",
                target_id = userId,
                action_at = DateTime.UtcNow,
                ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                user_agent = Request.Headers["User-Agent"].ToString()
            });
            await _context.SaveChangesAsync();

            return Ok();
        }

        private async Task<(User user, bool isNew)> SyncAdUser(AdAuthResult adResult)
        {
            var details = adResult.Details!;
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.username == details.Username);

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
                _context.Users.Add(user);
                _logger.LogInformation(
                    "Created new user from AD: {Username}, role: {Role}",
                    details.Username, adResult.Role);
                return (user, true);
            }

            user.full_name = details.DisplayName;
            user.email = details.Email;
            user.role = (adResult.Role ?? "").ToLowerInvariant();
            user.is_active = true;
            user.department = details.Department;
            user.mobile = details.Mobile;
            user.job_title = details.JobTitle;
            _logger.LogInformation(
                "Updated user from AD: {Username}, role: {Role}",
                details.Username, adResult.Role);

            return (user, false);
        }

        private void AddAuditLog(int userId, string action, string targetTable, int targetId)
        {
            if (userId <= 0) return;
            _context.AuditLogs.Add(new AuditLog
            {
                user_id = userId,
                action = action,
                target_table = targetTable,
                target_id = targetId,
                action_at = DateTime.UtcNow,
                ip_address = HttpContext.Connection.RemoteIpAddress?.ToString(),
                user_agent = Request.Headers["User-Agent"].ToString()
            });
        }

        private string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.username!),
                new Claim(ClaimTypes.Role, (user.role ?? "User").ToLowerInvariant())
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }

    public class SetPasswordRequest
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
    }
}
