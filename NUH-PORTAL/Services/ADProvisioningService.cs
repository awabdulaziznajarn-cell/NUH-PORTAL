using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using System.Security.Cryptography;

namespace NUH_PORTAL.Services
{
    public class ADProvisioningService
    {
        private readonly ActiveDirectoryService _adService;
        private readonly AppDbContext _db;
        private readonly ILogger<ADProvisioningService> _logger;
        private readonly ActiveDirectoryConfig _adConfig;

        public ADProvisioningService(ActiveDirectoryService adService, AppDbContext db, ILogger<ADProvisioningService> logger, IOptions<ActiveDirectoryConfig> adConfig)
        {
            _adService = adService;
            _db = db;
            _logger = logger;
            _adConfig = adConfig.Value;
        }

        public async Task<ADProvisioningResult> ProvisionAsync(Student student, int actorId, string? ipAddress = null, string? userAgent = null)
        {
            var result = new ADProvisioningResult();

            var samAccountName = "h" + student.student_id;
            var upn = $"{samAccountName}@{_adConfig.Domain}";
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)) + "!x1";

            var existing = await _adService.GetUserBySamAccountNameAsync(samAccountName);
            if (existing.Success)
            {
                result.Success = false;
                result.Error = $"AD account '{samAccountName}' already exists";
                _logger.LogWarning("AD provisioning skipped: {Sam} already exists for student {Id}", samAccountName, student.student_id);
                return result;
            }

            var nameParts = (student.full_name_english ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var givenName = nameParts.Length > 0 ? nameParts[0] : samAccountName;
            var sn = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : givenName;

            var targetOu = await GetOuForStudentAsync(student);
            var targetGroup = await GetGroupForStudentAsync(student);

            var userDn = $"CN={samAccountName},{targetOu}";

            var createRequest = new ADCreateUserRequest
            {
                SamAccountName = samAccountName,
                UserPrincipalName = upn,
                DisplayName = student.full_name_english ?? samAccountName,
                GivenName = givenName,
                Surname = sn,
                TargetOu = targetOu,
                Description = $"Housing Student - {student.college ?? "N/A"} - Level {student.academic_level ?? "N/A"}",
                UserAccountControl = 544
            };

            var createResult = await _adService.CreateADUserAsync(createRequest);
            if (!createResult.Success)
            {
                result.Success = false;
                result.Error = $"CreateUser failed: {createResult.Error}";
                result.StackTrace = createResult.StackTrace;
                _logger.LogError("AD provisioning failed at CreateUser for student {Id}: {Error} | StackTrace: {Stack}", student.student_id, createResult.Error, createResult.StackTrace);
                return result;
            }

            var passwordResult = await _adService.SetUserPasswordAsync(userDn, password);
            if (!passwordResult.Success)
            {
                await TryDeleteUser(userDn);
                result.Success = false;
                result.Error = $"SetPassword failed: {passwordResult.Error}";
                result.StackTrace = passwordResult.StackTrace;
                _logger.LogError("AD provisioning failed at SetPassword for student {Id}: {Error} | StackTrace: {Stack}", student.student_id, passwordResult.Error, passwordResult.StackTrace);
                return result;
            }

            var uacResult = await _adService.ModifyUserAccountControlAsync(userDn, 512);
            if (!uacResult.Success)
            {
                await TryDeleteUser(userDn);
                result.Success = false;
                result.Error = $"EnableAccount failed: {uacResult.Error}";
                result.StackTrace = uacResult.StackTrace;
                _logger.LogError("AD provisioning failed at EnableAccount for student {Id}: {Error} | StackTrace: {Stack}", student.student_id, uacResult.Error, uacResult.StackTrace);
                return result;
            }

            var groupResult = await _adService.AddUserToGroupAsync(userDn, targetGroup);
            if (!groupResult.Success)
            {
                await TryDeleteUser(userDn);
                result.Success = false;
                result.Error = $"AddToGroup failed: {groupResult.Error}";
                result.StackTrace = groupResult.StackTrace;
                _logger.LogError("AD provisioning failed at AddToGroup for student {Id}: {Error} | StackTrace: {Stack}", student.student_id, groupResult.Error, groupResult.StackTrace);
                return result;
            }

            await SetExtensionAttributesAsync(userDn, student);

            student.ad_username = samAccountName;
            student.ad_status = AdStatus.enabled;
            student.ad_last_sync_at = DateTime.UtcNow;

            var statusAction = await _db.StudentStatusActions
                .FirstOrDefaultAsync(s => s.StudentId == student.Id && s.StatusType == "ad_provisioning");
            if (statusAction == null)
            {
                statusAction = new StudentStatusAction
                {
                    StudentId = student.Id,
                    StudentNumber = student.student_id,
                    StatusType = "ad_provisioning",
                    CreatedBy = actorId,
                    CreatedDate = DateTime.UtcNow
                };
                _db.StudentStatusActions.Add(statusAction);
            }
            statusAction.PendingADAction = false;
            statusAction.ADActionCompleted = true;
            statusAction.ADActionDate = DateTime.UtcNow;

            LogLifecycleEvent(student.Id, "provisioned", actorId, $"AD account created: {samAccountName}", ipAddress);

            _db.AuditLogs.Add(new AuditLog
            {
                user_id = actorId,
                action = "ad_account_created",
                target_table = "Students",
                target_id = student.Id,
                action_at = DateTime.UtcNow,
                ip_address = ipAddress,
                user_agent = userAgent
            });

            await _db.SaveChangesAsync();

            result.Success = true;
            result.SamAccountName = samAccountName;
            result.UserDn = userDn;
            result.GroupDn = targetGroup;
            _logger.LogInformation("AD provisioning succeeded for student {Id}: {Sam} -> {Group}", student.student_id, samAccountName, targetGroup);
            return result;
        }

        public async Task<ADProvisioningResult> ReProvisionAsync(Student student, int actorId, string? ipAddress = null)
        {
            var result = new ADProvisioningResult();

            var samAccountName = student.ad_username ?? "h" + student.student_id;

            var existing = await _adService.GetUserBySamAccountNameAsync(samAccountName);
            if (!existing.Success)
            {
                result.Success = false;
                result.Error = $"AD account '{samAccountName}' not found. Use initial provisioning instead.";
                _logger.LogWarning("AD re-provisioning failed: {Sam} not found for student {Id}", samAccountName, student.student_id);
                return result;
            }

            var userDn = existing.DistinguishedName ?? throw new InvalidOperationException($"AD account '{samAccountName}' has no distinguished name");

            var targetOu = await GetOuForStudentAsync(student);
            var currentOu = userDn.Substring(userDn.IndexOf(',') + 1);

            if (!string.Equals(currentOu, targetOu, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Moving AD user {Sam} from {Current} to {Target}", samAccountName, currentOu, targetOu);
                var moveResult = await _adService.MoveUserAsync(userDn, targetOu);
                if (!moveResult.Success)
                {
                    result.Success = false;
                    result.Error = $"MoveUser failed: {moveResult.Error}";
                    _logger.LogError("AD re-provisioning failed at MoveUser for {Sam}: {Error}", samAccountName, moveResult.Error);
                    return result;
                }
                userDn = moveResult.NewDistinguishedName!;
            }

            var targetGroup = await GetGroupForStudentAsync(student);
            if (existing.MemberOf != null && !existing.MemberOf.Any(m => m.Contains(targetGroup.Split(',').FirstOrDefault()?.Replace("CN=", "") ?? "")))
            {
                var addGroupResult = await _adService.AddUserToGroupAsync(userDn, targetGroup);
                if (!addGroupResult.Success)
                {
                    _logger.LogWarning("AD re-provisioning: failed to add {Sam} to group {Group}: {Error}", samAccountName, targetGroup, addGroupResult.Error);
                }
            }

            var descResult = await _adService.SetUserExtensionAttributesAsync(userDn, new Dictionary<string, string>
            {
                { "description", $"Housing Student - {student.college ?? "N/A"} - Level {student.academic_level ?? "N/A"}" }
            });
            if (!descResult.Success)
                _logger.LogWarning("AD re-provisioning: failed to update description for {Sam}: {Error}", samAccountName, descResult.Error);

            await SetExtensionAttributesAsync(userDn, student);

            student.ad_status = AdStatus.enabled;
            student.ad_last_sync_at = DateTime.UtcNow;

            LogLifecycleEvent(student.Id, "reprovisioned", actorId, $"AD account re-provisioned: {samAccountName}", ipAddress);

            await _db.SaveChangesAsync();

            result.Success = true;
            result.SamAccountName = samAccountName;
            result.UserDn = userDn;
            result.GroupDn = targetGroup;
            _logger.LogInformation("AD re-provisioning succeeded for student {Id}: {Sam}", student.student_id, samAccountName);
            return result;
        }

        public async Task<ADProvisioningResult> SyncExtensionAttributesAsync(Student student, int actorId)
        {
            var result = new ADProvisioningResult();

            var samAccountName = student.ad_username;
            if (string.IsNullOrEmpty(samAccountName))
            {
                result.Success = false;
                result.Error = "Student has no ad_username";
                return result;
            }

            var existing = await _adService.GetUserBySamAccountNameAsync(samAccountName);
            if (!existing.Success)
            {
                result.Success = false;
                result.Error = $"AD account '{samAccountName}' not found";
                return result;
            }

            var userDn = existing.DistinguishedName ?? throw new InvalidOperationException($"AD account '{samAccountName}' has no distinguished name");

            await SetExtensionAttributesAsync(userDn, student);

            student.ad_last_sync_at = DateTime.UtcNow;

            LogLifecycleEvent(student.Id, "extension_attrs_synced", actorId, $"Extension attributes synced for {samAccountName}");

            await _db.SaveChangesAsync();

            result.Success = true;
            result.SamAccountName = samAccountName;
            result.UserDn = userDn;
            return result;
        }

        private async Task SetExtensionAttributesAsync(string userDn, Student student)
        {
            var attrMapping = new Dictionary<string, string?>
            {
                { await GetExtAttrKeyAsync("extensionAttribute1", "extensionAttribute1"), student.phone },
                { await GetExtAttrKeyAsync("extensionAttribute2", "extensionAttribute2"), student.housing_building },
                { await GetExtAttrKeyAsync("extensionAttribute3", "extensionAttribute3"), student.room_number },
                { await GetExtAttrKeyAsync("extensionAttribute4", "extensionAttribute4"), student.apartment_number },
                { await GetExtAttrKeyAsync("extensionAttribute5", "extensionAttribute5"), student.college },
                { await GetExtAttrKeyAsync("extensionAttribute6", "extensionAttribute6"), student.department },
                { await GetExtAttrKeyAsync("extensionAttribute7", "extensionAttribute7"), student.academic_level }
            };

            var attrsToSet = new Dictionary<string, string>();
            foreach (var kvp in attrMapping)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                    attrsToSet[kvp.Key] = kvp.Value;
            }

            if (attrsToSet.Count > 0)
            {
                var extResult = await _adService.SetUserExtensionAttributesAsync(userDn, attrsToSet);
                if (!extResult.Success)
                    _logger.LogWarning("Failed to set extension attributes for {Dn}: {Error}", userDn, extResult.Error);
            }
        }

        private async Task<string> GetExtAttrKeyAsync(string configKey, string defaultValue)
        {
            var config = await _db.ADConfigurations
                .FirstOrDefaultAsync(c => c.ConfigKey == configKey);
            return config?.ConfigValue ?? defaultValue;
        }

        private async Task<string> GetOuForStudentAsync(Student student)
        {
            var config = await _db.ADConfigurations.FirstOrDefaultAsync(c => c.ConfigKey == ADConfigurationKeys.StudentOuPath);
            var baseOu = config?.ConfigValue ?? "OU=New,OU=Students,DC=globalgroups,DC=com";

            var isMale = student.gender == Gender.Male;
            var genderOu = isMale ? "Male" : "Female";

            if (baseOu.Contains("OU=New"))
            {
                return $"OU={genderOu},{baseOu}";
            }

            return $"OU={genderOu},OU=New,{baseOu}";
        }

        private async Task<string> GetGroupForStudentAsync(Student student)
        {
            var configMale = await _db.ADConfigurations.FirstOrDefaultAsync(c => c.ConfigKey == "male_group_dn");
            var configFemale = await _db.ADConfigurations.FirstOrDefaultAsync(c => c.ConfigKey == "female_group_dn");

            if (configMale != null && configFemale != null)
            {
                var isMale = student.gender == Gender.Male;
                return isMale
                    ? (configMale.ConfigValue ?? "CN=NUH-Student-B,OU=Groups,DC=globalgroups,DC=com")
                    : (configFemale.ConfigValue ?? "CN=NUH-Student-G,OU=Groups,DC=globalgroups,DC=com");
            }

            var isMaleFallback = student.gender == Gender.Male;
            return isMaleFallback
                ? "CN=NUH-Student-B,OU=Groups,DC=globalgroups,DC=com"
                : "CN=NUH-Student-G,OU=Groups,DC=globalgroups,DC=com";
        }

        private void LogLifecycleEvent(int studentId, string action, int performedBy, string details, string? ipAddress = null)
        {
            _db.AccountLifecycleLogs.Add(new AccountLifecycleLog
            {
                StudentId = studentId,
                Action = action,
                PerformedBy = performedBy,
                PerformedAt = DateTime.UtcNow,
                Details = details,
                IpAddress = ipAddress
            });
        }

        private async Task TryDeleteUser(string userDn)
        {
            try
            {
                await _adService.DeleteADUserAsync(userDn);
                _logger.LogWarning("AD rollback: deleted user {Dn}", userDn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AD rollback failed to delete user {Dn}", userDn);
            }
        }
    }

    public class ADProvisioningResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? StackTrace { get; set; }
        public string? SamAccountName { get; set; }
        public string? UserDn { get; set; }
        public string? GroupDn { get; set; }
    }
}
