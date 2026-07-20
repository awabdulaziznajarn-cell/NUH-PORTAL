using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services;
using System.Security.Claims;

namespace NUH_PORTAL.Controllers
{
    [Authorize(Roles = "admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class HousingAccountManagementController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ActiveDirectoryService _adService;
        private readonly ADProvisioningService _adProvisioning;
        private readonly ILogger<HousingAccountManagementController> _logger;

        public HousingAccountManagementController(
            AppDbContext context,
            ActiveDirectoryService adService,
            ADProvisioningService adProvisioning,
            ILogger<HousingAccountManagementController> logger)
        {
            _context = context;
            _adService = adService;
            _adProvisioning = adProvisioning;
            _logger = logger;
        }

        private int CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private string CurrentUserRole =>
            User.FindFirst(ClaimTypes.Role)?.Value?.ToLower() ?? "";

        private string ClientIp =>
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAll([FromQuery] string? status)
        {
            var query = _context.Students
                .Where(s => !s.IsDeleted && s.ad_username != null);

            if (!string.IsNullOrEmpty(status))
            {
                query = status.ToLower() switch
                {
                    "enabled" => query.Where(s => s.ad_status == "enabled"),
                    "disabled" => query.Where(s => s.ad_status == "disabled"),
                    "unknown" => query.Where(s => s.ad_status == null || s.ad_status == ""),
                    _ => query
                };
            }

            var students = await query.OrderBy(s => s.full_name).ToListAsync();

            return Ok(students.Select(s => new
            {
                s.Id,
                s.student_id,
                s.full_name,
                s.full_name_english,
                s.college,
                s.department,
                s.gender,
                s.housing_building,
                s.room_number,
                s.ad_username,
                s.ad_status,
                s.ad_last_sync_at,
                s.status,
                s.student_status
            }));
        }

        [HttpGet("{studentId}")]
        public async Task<IActionResult> GetDetails(int studentId)
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == studentId && !s.IsDeleted);

            if (student == null)
                return NotFound(new { message = "Student not found" });

            object? adDetails = null;
            if (!string.IsNullOrEmpty(student.ad_username))
            {
                var adResult = await _adService.GetUserBySamAccountNameAsync(student.ad_username);
                if (adResult.Success)
                {
                    adDetails = new
                    {
                        adResult.DistinguishedName,
                        adResult.SamAccountName,
                        adResult.UserPrincipalName,
                        adResult.DisplayName,
                        adResult.AccountEnabled,
                        adResult.UserAccountControl,
                        adResult.Department,
                        adResult.Description,
                        adResult.MemberOf,
                        adResult.ExtensionAttribute1
                    };
                }
            }

            var lifecycleLogs = await _context.AccountLifecycleLogs
                .Where(l => l.StudentId == student.Id)
                .OrderByDescending(l => l.PerformedAt)
                .Take(50)
                .Select(l => new
                {
                    l.Id,
                    l.Action,
                    l.PerformedAt,
                    l.Details,
                    l.IpAddress,
                    PerformerName = l.Performer != null ? l.Performer.full_name : ""
                })
                .ToListAsync();

            return Ok(new
            {
                Student = new
                {
                    student.Id,
                    student.student_id,
                    student.full_name,
                    student.full_name_english,
                    student.college,
                    student.department,
                    student.gender,
                    student.academic_level,
                    student.phone,
                    student.housing_building,
                    student.room_number,
                    student.apartment_number,
                    student.ad_username,
                    student.ad_status,
                    student.ad_last_sync_at,
                    student.status,
                    student.student_status
                },
                AdDetails = adDetails,
                LifecycleLogs = lifecycleLogs
            });
        }

        [HttpPost("{studentId}/enable")]
        public async Task<IActionResult> EnableAccount(int studentId)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null || string.IsNullOrEmpty(student.ad_username))
                return BadRequest(new { message = "Student has no AD account" });

            var adResult = await _adService.GetUserBySamAccountNameAsync(student.ad_username);
            if (!adResult.Success || string.IsNullOrEmpty(adResult.DistinguishedName))
                return BadRequest(new { message = "AD account not found", details = adResult.Error });

            var result = await _adService.EnableUserAsync(adResult.DistinguishedName);
            if (!result.Success)
                return StatusCode(500, new { message = "Failed to enable AD account", error = result.Error });

            student.ad_status = "enabled";
            student.ad_last_sync_at = DateTime.UtcNow;

            _context.AccountLifecycleLogs.Add(new AccountLifecycleLog
            {
                StudentId = studentId,
                Action = "enabled",
                PerformedBy = CurrentUserId,
                PerformedAt = DateTime.UtcNow,
                Details = $"AD account enabled by {CurrentUserRole}",
                IpAddress = ClientIp
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("AD account enabled for student {Id}: {Sam}", studentId, student.ad_username);
            return Ok(new { message = "Account enabled", ad_status = "enabled" });
        }

        [HttpPost("{studentId}/disable")]
        public async Task<IActionResult> DisableAccount(int studentId)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null || string.IsNullOrEmpty(student.ad_username))
                return BadRequest(new { message = "Student has no AD account" });

            var adResult = await _adService.GetUserBySamAccountNameAsync(student.ad_username);
            if (!adResult.Success || string.IsNullOrEmpty(adResult.DistinguishedName))
                return BadRequest(new { message = "AD account not found", details = adResult.Error });

            var result = await _adService.DisableUserAsync(adResult.DistinguishedName);
            if (!result.Success)
                return StatusCode(500, new { message = "Failed to disable AD account", error = result.Error });

            student.ad_status = "disabled";
            student.ad_last_sync_at = DateTime.UtcNow;

            _context.AccountLifecycleLogs.Add(new AccountLifecycleLog
            {
                StudentId = studentId,
                Action = "disabled",
                PerformedBy = CurrentUserId,
                PerformedAt = DateTime.UtcNow,
                Details = $"AD account disabled by {CurrentUserRole}",
                IpAddress = ClientIp
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("AD account disabled for student {Id}: {Sam}", studentId, student.ad_username);
            return Ok(new { message = "Account disabled", ad_status = "disabled" });
        }

        [HttpPost("{studentId}/reset-password")]
        public async Task<IActionResult> ResetPassword(int studentId, [FromBody] ResetPasswordDto dto)
        {
            if (string.IsNullOrEmpty(dto.NewPassword) || dto.NewPassword.Length < 8)
                return BadRequest(new { message = "Password must be at least 8 characters" });

            var student = await _context.Students.FindAsync(studentId);
            if (student == null || string.IsNullOrEmpty(student.ad_username))
                return BadRequest(new { message = "Student has no AD account" });

            var adResult = await _adService.GetUserBySamAccountNameAsync(student.ad_username);
            if (!adResult.Success || string.IsNullOrEmpty(adResult.DistinguishedName))
                return BadRequest(new { message = "AD account not found", details = adResult.Error });

            var compatResult = await _adService.ValidatePasswordCompatibilityAsync(dto.NewPassword);
            if (!compatResult.Compatible)
                return BadRequest(new { message = "Password does not meet AD policy", issues = compatResult.Issues });

            var result = await _adService.SetUserPasswordAsync(adResult.DistinguishedName, dto.NewPassword);
            if (!result.Success)
                return StatusCode(500, new { message = "Failed to reset password", error = result.Error });

            student.ad_last_sync_at = DateTime.UtcNow;

            _context.AccountLifecycleLogs.Add(new AccountLifecycleLog
            {
                StudentId = studentId,
                Action = "password_reset",
                PerformedBy = CurrentUserId,
                PerformedAt = DateTime.UtcNow,
                Details = $"Password reset by {CurrentUserRole}",
                IpAddress = ClientIp
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Password reset for student {Id}: {Sam}", studentId, student.ad_username);
            return Ok(new { message = "Password reset successfully" });
        }

        [HttpPost("{studentId}/re-provision")]
        public async Task<IActionResult> ReProvision(int studentId)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
                return NotFound(new { message = "Student not found" });

            var result = await _adProvisioning.ReProvisionAsync(student, CurrentUserId, ClientIp);
            if (!result.Success)
                return StatusCode(500, new { message = "Re-provisioning failed", error = result.Error });

            await _context.SaveChangesAsync();

            _logger.LogInformation("AD account re-provisioned for student {Id}", studentId);
            return Ok(new { message = "Account re-provisioned", samAccountName = result.SamAccountName });
        }

        [HttpPost("{studentId}/sync-attrs")]
        public async Task<IActionResult> SyncExtensionAttributes(int studentId)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
                return NotFound(new { message = "Student not found" });

            var result = await _adProvisioning.SyncExtensionAttributesAsync(student, CurrentUserId);
            if (!result.Success)
                return StatusCode(500, new { message = "Sync failed", error = result.Error });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Extension attributes synced for student {Id}", studentId);
            return Ok(new { message = "Extension attributes synced" });
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchADUsers([FromQuery] string q, [FromQuery] int max = 50)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new { users = new List<object>() });

            max = Math.Min(max, 200);
            var result = await _adService.SearchUsersAsync(q, max);
            if (!result.Success)
                return StatusCode(500, new { message = "Search failed", error = result.Error });

            return Ok(new { users = result.Users, total = result.TotalResults });
        }

        [HttpGet("lifecycle/{studentId}")]
        public async Task<IActionResult> GetLifecycleLogs(int studentId, [FromQuery] int limit = 50)
        {
            var logs = await _context.AccountLifecycleLogs
                .Where(l => l.StudentId == studentId)
                .OrderByDescending(l => l.PerformedAt)
                .Take(limit)
                .Select(l => new
                {
                    l.Id,
                    l.Action,
                    l.PerformedAt,
                    l.Details,
                    l.IpAddress,
                    PerformerName = l.Performer != null ? l.Performer.full_name : ""
                })
                .ToListAsync();

            return Ok(new { logs });
        }

        [HttpGet("ad-config")]
        public async Task<IActionResult> GetAdConfig()
        {
            var configs = await _context.ADConfigurations
                .OrderBy(c => c.ConfigKey)
                .ToListAsync();

            return Ok(new { configs });
        }

        [HttpPost("ad-config")]
        public async Task<IActionResult> UpdateAdConfig([FromBody] List<AdConfigDto> configs)
        {
            if (CurrentUserRole != "admin")
                return Forbid();

            foreach (var dto in configs)
            {
                var existing = await _context.ADConfigurations
                    .FirstOrDefaultAsync(c => c.ConfigKey == dto.ConfigKey);

                if (existing != null)
                {
                    existing.ConfigValue = dto.ConfigValue;
                    existing.Description = dto.Description ?? existing.Description;
                    existing.UpdatedBy = CurrentUserId;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.ADConfigurations.Add(new ADConfiguration
                    {
                        ConfigKey = dto.ConfigKey,
                        ConfigValue = dto.ConfigValue,
                        Description = dto.Description,
                        UpdatedBy = CurrentUserId,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("AD configuration updated by user {UserId}", CurrentUserId);

            return Ok(new { message = "Configuration updated" });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetHousingStats()
        {
            var baseQuery = _context.Students.Where(s => !s.IsDeleted && s.ad_username != null);

            var stats = new
            {
                with_accounts = await baseQuery.CountAsync(),
                enabled = await baseQuery.CountAsync(s => s.ad_status == "enabled"),
                disabled = await baseQuery.CountAsync(s => s.ad_status == "disabled"),
                unknown_status = await baseQuery.CountAsync(s => s.ad_status == null || s.ad_status == ""),
                synced_last_24h = await baseQuery.CountAsync(s =>
                    s.ad_last_sync_at != null && s.ad_last_sync_at >= DateTime.UtcNow.AddHours(-24)),
                not_synced = await baseQuery.CountAsync(s =>
                    s.ad_last_sync_at == null),
                total_students = await _context.Students.CountAsync(s => !s.IsDeleted),
                with_username = await baseQuery.CountAsync(),
                without_username = await _context.Students.CountAsync(s =>
                    !s.IsDeleted && (s.ad_username == null || s.ad_username == ""))
            };

            return Ok(stats);
        }
    }

    public class ResetPasswordDto
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AdConfigDto
    {
        public string ConfigKey { get; set; } = string.Empty;
        public string ConfigValue { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
