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

            var (givenName, initials, sn) = SplitEnglishName(student.full_name_english, samAccountName);

            var targetOu = await GetOuForStudentAsync(student);
            var targetGroup = await GetGroupForStudentAsync(student);
            var (companyAr, departmentAr) = await GetOrgNamesAsync(student);

            var userDn = $"CN={samAccountName},{targetOu}";

            // معيار جامعة نجران لحسابات الطلاب:
            //   CN / sAMAccountName = h{الرقم الجامعي}   ·  UPN = h{الرقم}@nuh.edu.sa
            //   displayName = الاسم الإنجليزي            ·  description = الاسم العربي
            //   employeeID  = رقم الهوية                 ·  mobile      = الجوال
            //   company     = الكلية بالعربي             ·  department  = القسم بالعربي
            var createRequest = new ADCreateUserRequest
            {
                SamAccountName = samAccountName,
                UserPrincipalName = upn,
                DisplayName = student.full_name_english ?? samAccountName,
                GivenName = givenName,
                Initials = initials,
                Surname = sn,
                TargetOu = targetOu,
                Description = student.full_name,
                EmployeeId = student.national_id,
                Mobile = student.phone,
                Company = companyAr,
                Department = departmentAr,
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

        // مزامنة بيانات الطالب على خصائص AD القياسية حسب معيار الجامعة.
        //
        // ملاحظة: كان هذا التابع يكتب في extensionAttribute1-7، وهي خصائص
        // غير موجودة في schema دومين nuh.edu.sa (تأتي مع امتداد Exchange)،
        // فكانت كل عملية إنشاء تفشل في هذه الخطوة وتُسجَّل كتحذير.
        private async Task SetExtensionAttributesAsync(string userDn, Student student)
        {
            var (companyAr, departmentAr) = await GetOrgNamesAsync(student);
            var (givenName, initials, sn) = SplitEnglishName(student.full_name_english, student.ad_username ?? "");

            var attrMapping = new Dictionary<string, string?>
            {
                ["description"] = student.full_name,            // الاسم العربي الكامل
                ["displayName"] = student.full_name_english,    // الاسم الإنجليزي الكامل
                ["givenName"]   = givenName,
                ["initials"]    = initials,
                ["sn"]          = sn,
                ["employeeID"]  = student.national_id,
                ["mobile"]      = student.phone,
                ["company"]     = companyAr,
                ["department"]  = departmentAr
            };

            var attrsToSet = attrMapping
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value!);

            if (attrsToSet.Count == 0) return;

            var extResult = await _adService.SetUserExtensionAttributesAsync(userDn, attrsToSet);
            if (!extResult.Success)
                _logger.LogWarning("Failed to sync AD attributes for {Dn}: {Error}", userDn, extResult.Error);
        }

        // تفكيك الاسم الإنجليزي حسب معيار الجامعة:
        //   "MOHAMMED SALEM ALSAIARI"          → (MOHAMMED, "S",  ALSAIARI)
        //   "MOHAMMED SALEM MOHAMMED ALSAIARI" → (MOHAMMED, "SM", ALSAIARI)
        //   "AHMED ALI"                        → (AHMED,    null, ALI)
        private static (string given, string? initials, string sn) SplitEnglishName(string? fullNameEn, string fallback)
        {
            var parts = (fullNameEn ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0) return (fallback, null, fallback);
            if (parts.Length == 1) return (parts[0], null, parts[0]);
            if (parts.Length == 2) return (parts[0], null, parts[1]);

            // الأحرف الأولى للأسماء الوسطى — خاصية initials في AD محدودة بـ 6 أحرف
            var middle = string.Concat(parts[1..^1].Select(p => char.ToUpperInvariant(p[0])));
            if (middle.Length > 6) middle = middle[..6];

            return (parts[0], middle, parts[^1]);
        }

        // الكلية والقسم بالعربي من جداول القوائم المرجعية.
        // أعمدة student.college / student.department النصية تخزّن أكوادًا
        // (engineering, cs ...) وليس أسماء، فنقرأ الاسم من الـ FK.
        private async Task<(string? company, string? department)> GetOrgNamesAsync(Student student)
        {
            string? company = null;
            string? department = null;

            if (student.CollegeId.HasValue)
                company = await _db.Colleges.AsNoTracking()
                    .Where(c => c.Id == student.CollegeId.Value)
                    .Select(c => c.ArName).FirstOrDefaultAsync();

            if (student.DepartmentId.HasValue)
                department = await _db.Departments.AsNoTracking()
                    .Where(d => d.Id == student.DepartmentId.Value)
                    .Select(d => d.ArName).FirstOrDefaultAsync();

            // احتياطي: لو الربط بالقوائم لم يُطبَّق بعد، نستخدم النص الخام
            return (string.IsNullOrWhiteSpace(company) ? student.college : company,
                    string.IsNullOrWhiteSpace(department) ? student.department : department);
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
