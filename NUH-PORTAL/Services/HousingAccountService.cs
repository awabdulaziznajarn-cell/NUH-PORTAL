using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Common.Pagination;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.Housing;
using NUH_PORTAL.DTOs.Students;
using NUH_PORTAL.Models;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // إدارة حسابات AD لطلاب السكن — اتنقل من HousingAccountManagementController
    public class HousingAccountService : AppServiceBase, IHousingAccountService
    {
        private readonly IRepository<Student> _students;
        private readonly IRepository<AccountLifecycleLog> _lifecycle;
        private readonly IRepository<ADConfiguration> _adConfigs;
        private readonly ActiveDirectoryService _adService;
        private readonly ADProvisioningService _adProvisioning;
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<HousingAccountService> _logger;

        public HousingAccountService(
            IRepository<Student> students,
            IRepository<AccountLifecycleLog> lifecycle,
            IRepository<ADConfiguration> adConfigs,
            ActiveDirectoryService adService,
            ADProvisioningService adProvisioning,
            IHttpContextAccessor http,
            ILogger<HousingAccountService> logger,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _lifecycle = lifecycle;
            _adConfigs = adConfigs;
            _adService = adService;
            _adProvisioning = adProvisioning;
            _http = http;
            _logger = logger;
        }

        private string ClientIp => _http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "";

        public async Task<List<HousingAccountListItemDto>> GetAllAsync(string? status)
        {
            return await BuildAccountsQuery(status)
                .OrderBy(s => s.full_name)
                .Select(s => new HousingAccountListItemDto
                {
                    Id = s.Id,
                    student_id = s.student_id,
                    full_name = s.full_name,
                    full_name_english = s.full_name_english,
                    college = s.college,
                    department = s.department,
                    gender = s.gender,
                    housing_building = s.housing_building,
                    room_number = s.room_number,
                    ad_username = s.ad_username,
                    ad_status = s.ad_status,
                    ad_last_sync_at = s.ad_last_sync_at,
                    status = s.status,
                    student_status = s.student_status
                })
                .ToListAsync();
        }


        public async Task<QueryResult<HousingAccountListItemDto>> GetPagedAsync(QueryParams queryParams, string? status)
        {
            var query = BuildAccountsQuery(status);

            var f = queryParams.FilterText?.Trim();
            if (!string.IsNullOrEmpty(f))
            {
                query = query.Where(s =>
                    (s.student_id != null && s.student_id.Contains(f)) ||
                    (s.full_name != null && s.full_name.Contains(f)) ||
                    (s.ad_username != null && s.ad_username.Contains(f)));
            }

            query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortAsc) switch
            {
                ("student_id", true) => query.OrderBy(s => s.student_id),
                ("student_id", false) => query.OrderByDescending(s => s.student_id),
                ("ad_status", true) => query.OrderBy(s => s.ad_status),
                ("ad_status", false) => query.OrderByDescending(s => s.ad_status),
                ("ad_last_sync_at", true) => query.OrderBy(s => s.ad_last_sync_at),
                ("ad_last_sync_at", false) => query.OrderByDescending(s => s.ad_last_sync_at),
                ("full_name", false) => query.OrderByDescending(s => s.full_name),
                _ => query.OrderBy(s => s.full_name)
            };

            var paged = await query
                .Select(s => new HousingAccountListItemDto
                {
                    Id = s.Id,
                    student_id = s.student_id,
                    full_name = s.full_name,
                    full_name_english = s.full_name_english,
                    college = s.college,
                    department = s.department,
                    gender = s.gender,
                    housing_building = s.housing_building,
                    room_number = s.room_number,
                    ad_username = s.ad_username,
                    ad_status = s.ad_status,
                    ad_last_sync_at = s.ad_last_sync_at,
                    status = s.status,
                    student_status = s.student_status
                })
                .ToPagedResultAsync(queryParams);

            return paged;
        }

        private IQueryable<Student> BuildAccountsQuery(string? status)
        {
            var query = _students.Query().AsNoTracking()
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

            return query;
        }

        public async Task<HousingAccountDetailsDto> GetDetailsAsync(int studentId)
        {
            var student = await _students.FindAsync(s => s.Id == studentId && !s.IsDeleted)
                ?? throw UserFriendlyException.NotFound("Student not found");

            AdAccountDetailsDto? adDetails = null;
            if (!string.IsNullOrEmpty(student.ad_username))
            {
                var adResult = await _adService.GetUserBySamAccountNameAsync(student.ad_username);
                if (adResult.Success)
                {
                    adDetails = new AdAccountDetailsDto
                    {
                        DistinguishedName = adResult.DistinguishedName,
                        SamAccountName = adResult.SamAccountName,
                        UserPrincipalName = adResult.UserPrincipalName,
                        DisplayName = adResult.DisplayName,
                        AccountEnabled = adResult.AccountEnabled,
                        UserAccountControl = adResult.UserAccountControl,
                        Department = adResult.Department,
                        Description = adResult.Description,
                        MemberOf = adResult.MemberOf,
                        ExtensionAttribute1 = adResult.ExtensionAttribute1
                    };
                }
            }

            var lifecycleLogs = await GetLifecycleLogsAsync(student.Id, 50);

            return new HousingAccountDetailsDto
            {
                Student = new HousingStudentDto
                {
                    Id = student.Id,
                    student_id = student.student_id,
                    full_name = student.full_name,
                    full_name_english = student.full_name_english,
                    college = student.college,
                    department = student.department,
                    gender = student.gender,
                    academic_level = student.academic_level,
                    phone = student.phone,
                    housing_building = student.housing_building,
                    room_number = student.room_number,
                    apartment_number = student.apartment_number,
                    ad_username = student.ad_username,
                    ad_status = student.ad_status,
                    ad_last_sync_at = student.ad_last_sync_at,
                    status = student.status,
                    student_status = student.student_status
                },
                AdDetails = adDetails,
                LifecycleLogs = lifecycleLogs
            };
        }

        public async Task<ToggleAccountResultDto> EnableAccountAsync(int studentId)
        {
            var (student, dn) = await GetStudentWithAdAccountAsync(studentId);

            var result = await _adService.EnableUserAsync(dn);
            if (!result.Success)
                throw new UserFriendlyException($"Failed to enable AD account: {result.Error}", 500);

            student.ad_status = "enabled";
            student.ad_last_sync_at = DateTime.UtcNow;

            await AddLifecycleLogAsync(studentId, "enabled", $"AD account enabled by {UnitOfWork.GetCurrentUserRole()?.ToLower()}");
            await UnitOfWork.SaveAsync();

            _logger.LogInformation("AD account enabled for student {Id}: {Sam}", studentId, student.ad_username);
            return new ToggleAccountResultDto { Message = "Account enabled", ad_status = "enabled" };
        }

        public async Task<ToggleAccountResultDto> DisableAccountAsync(int studentId)
        {
            var (student, dn) = await GetStudentWithAdAccountAsync(studentId);

            var result = await _adService.DisableUserAsync(dn);
            if (!result.Success)
                throw new UserFriendlyException($"Failed to disable AD account: {result.Error}", 500);

            student.ad_status = "disabled";
            student.ad_last_sync_at = DateTime.UtcNow;

            await AddLifecycleLogAsync(studentId, "disabled", $"AD account disabled by {UnitOfWork.GetCurrentUserRole()?.ToLower()}");
            await UnitOfWork.SaveAsync();

            _logger.LogInformation("AD account disabled for student {Id}: {Sam}", studentId, student.ad_username);
            return new ToggleAccountResultDto { Message = "Account disabled", ad_status = "disabled" };
        }

        public async Task ResetPasswordAsync(int studentId, ResetPasswordDto dto)
        {
            if (string.IsNullOrEmpty(dto.NewPassword) || dto.NewPassword.Length < 8)
                throw new UserFriendlyException("Password must be at least 8 characters", 400);

            var (student, dn) = await GetStudentWithAdAccountAsync(studentId);

            var compatResult = await _adService.ValidatePasswordCompatibilityAsync(dto.NewPassword);
            if (!compatResult.Compatible)
                throw new UserFriendlyException(
                    "Password does not meet AD policy" +
                    (compatResult.Issues is { Count: > 0 } ? ": " + string.Join("، ", compatResult.Issues) : ""), 400);

            var result = await _adService.SetUserPasswordAsync(dn, dto.NewPassword);
            if (!result.Success)
                throw new UserFriendlyException($"Failed to reset password: {result.Error}", 500);

            student.ad_last_sync_at = DateTime.UtcNow;

            await AddLifecycleLogAsync(studentId, "password_reset", $"Password reset by {UnitOfWork.GetCurrentUserRole()?.ToLower()}");
            await UnitOfWork.SaveAsync();

            _logger.LogInformation("Password reset for student {Id}: {Sam}", studentId, student.ad_username);
        }

        public async Task<ReProvisionResultDto> ReProvisionAsync(int studentId)
        {
            var student = await _students.GetByIdAsync(studentId)
                ?? throw UserFriendlyException.NotFound("Student not found");

            var result = await _adProvisioning.ReProvisionAsync(student, UnitOfWork.GetCurrentUserId(), ClientIp);
            if (!result.Success)
                throw new UserFriendlyException($"Re-provisioning failed: {result.Error}", 500);

            await UnitOfWork.SaveAsync();

            _logger.LogInformation("AD account re-provisioned for student {Id}", studentId);
            return new ReProvisionResultDto { Message = "Account re-provisioned", SamAccountName = result.SamAccountName };
        }

        public async Task SyncExtensionAttributesAsync(int studentId)
        {
            var student = await _students.GetByIdAsync(studentId)
                ?? throw UserFriendlyException.NotFound("Student not found");

            var result = await _adProvisioning.SyncExtensionAttributesAsync(student, UnitOfWork.GetCurrentUserId());
            if (!result.Success)
                throw new UserFriendlyException($"Sync failed: {result.Error}", 500);

            await UnitOfWork.SaveAsync();

            _logger.LogInformation("Extension attributes synced for student {Id}", studentId);
        }

        public async Task<AdSearchResultDto> SearchADUsersAsync(string? q, int max)
        {
            if (string.IsNullOrWhiteSpace(q))
                return new AdSearchResultDto { Users = new List<object>(), Total = 0 };

            max = Math.Min(max, 200);
            var result = await _adService.SearchUsersAsync(q, max);
            if (!result.Success)
                throw new UserFriendlyException($"Search failed: {result.Error}", 500);

            return new AdSearchResultDto { Users = result.Users, Total = result.TotalResults };
        }

        public async Task<List<LifecycleLogDto>> GetLifecycleLogsAsync(int studentId, int limit)
        {
            return await _lifecycle.Query().AsNoTracking()
                .Where(l => l.StudentId == studentId)
                .OrderByDescending(l => l.PerformedAt)
                .Take(limit)
                .Select(l => new LifecycleLogDto
                {
                    Id = l.Id,
                    Action = l.Action,
                    PerformedAt = l.PerformedAt,
                    Details = l.Details,
                    IpAddress = l.IpAddress,
                    PerformerName = l.Performer != null ? l.Performer.full_name : ""
                })
                .ToListAsync();
        }

        public async Task<List<AdConfigurationDto>> GetAdConfigAsync()
        {
            return await _adConfigs.Query().AsNoTracking()
                .OrderBy(c => c.ConfigKey)
                .Select(c => new AdConfigurationDto
                {
                    Id = c.Id,
                    ConfigKey = c.ConfigKey,
                    ConfigValue = c.ConfigValue,
                    Description = c.Description,
                    UpdatedBy = c.UpdatedBy,
                    UpdatedAt = c.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task UpdateAdConfigAsync(List<AdConfigDto> configs)
        {
            if (UnitOfWork.GetCurrentUserRole()?.ToLower() != "admin")
                throw UserFriendlyException.Forbidden();

            var actorId = UnitOfWork.GetCurrentUserId();

            foreach (var dto in configs)
            {
                var existing = await _adConfigs.FindAsync(c => c.ConfigKey == dto.ConfigKey);

                if (existing != null)
                {
                    existing.ConfigValue = dto.ConfigValue;
                    existing.Description = dto.Description ?? existing.Description;
                    existing.UpdatedBy = actorId;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    await _adConfigs.AddAsync(new ADConfiguration
                    {
                        ConfigKey = dto.ConfigKey,
                        ConfigValue = dto.ConfigValue,
                        Description = dto.Description,
                        UpdatedBy = actorId,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await UnitOfWork.SaveAsync();
            _logger.LogInformation("AD configuration updated by user {UserId}", actorId);
        }

        public async Task<HousingStatsDto> GetHousingStatsAsync()
        {
            var baseQuery = _students.Query().AsNoTracking().Where(s => !s.IsDeleted && s.ad_username != null);

            return new HousingStatsDto
            {
                with_accounts = await baseQuery.CountAsync(),
                enabled = await baseQuery.CountAsync(s => s.ad_status == "enabled"),
                disabled = await baseQuery.CountAsync(s => s.ad_status == "disabled"),
                unknown_status = await baseQuery.CountAsync(s => s.ad_status == null || s.ad_status == ""),
                synced_last_24h = await baseQuery.CountAsync(s => s.ad_last_sync_at != null && s.ad_last_sync_at >= DateTime.UtcNow.AddHours(-24)),
                not_synced = await baseQuery.CountAsync(s => s.ad_last_sync_at == null),
                total_students = await _students.Query().AsNoTracking().CountAsync(s => !s.IsDeleted),
                with_username = await baseQuery.CountAsync(),
                without_username = await _students.Query().AsNoTracking().CountAsync(s => !s.IsDeleted && (s.ad_username == null || s.ad_username == ""))
            };
        }

        // ----------------------------- Helpers -----------------------------

        // بيجيب الطالب + الـ DN بتاع حسابه في AD أو يرمي الخطأ المناسب
        private async Task<(Student student, string dn)> GetStudentWithAdAccountAsync(int studentId)
        {
            var student = await _students.GetByIdAsync(studentId);
            if (student == null || string.IsNullOrEmpty(student.ad_username))
                throw new UserFriendlyException("Student has no AD account", 400);

            var adResult = await _adService.GetUserBySamAccountNameAsync(student.ad_username);
            if (!adResult.Success || string.IsNullOrEmpty(adResult.DistinguishedName))
            {
                _logger.LogWarning("AD account not found for student {Id}: {Error}", studentId, adResult.Error);
                throw new UserFriendlyException("AD account not found", 400);
            }

            return (student, adResult.DistinguishedName);
        }

        private async Task AddLifecycleLogAsync(int studentId, string action, string details)
        {
            await _lifecycle.AddAsync(new AccountLifecycleLog
            {
                StudentId = studentId,
                Action = action,
                PerformedBy = UnitOfWork.GetCurrentUserId(),
                PerformedAt = DateTime.UtcNow,
                Details = details,
                IpAddress = ClientIp
            });
        }
    }
}
