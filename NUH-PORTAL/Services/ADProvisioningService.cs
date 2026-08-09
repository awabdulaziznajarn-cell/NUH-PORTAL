using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NUH_PORTAL.Data;
using NUH_PORTAL.DTOs.Housing;
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

            LogLifecycleEvent(student.Id, "provisioned", actorId, $"تم إنشاء حساب الشبكة: {samAccountName}", ipAddress);

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

            LogLifecycleEvent(student.Id, "reprovisioned", actorId, $"تمت إعادة إنشاء حساب الشبكة: {samAccountName}", ipAddress);

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

            LogLifecycleEvent(student.Id, "extension_attrs_synced", actorId, $"تمت مزامنة الخصائص الإضافية للحساب: {samAccountName}");

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

            // حرف واحد فقط — أول حرف من اسم الأب (الاسم اللي بعد الأول مباشرة).
            // كان بياخد أول حرف من *كل* الأسماء الوسطى، فاسم زي
            // "MAHMOUD MOHAMED AHMED RASHED" كان بيطلع initials = "MA".
            var middle = char.ToUpperInvariant(parts[1][0]).ToString();

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

        // ─────────────────────────────────────────────────────────────────────
        //  مزامنة حسابات الشبكة الموجودة أصلاً
        // ---------------------------------------------------------------------
        //  طلاب الجامعة عندهم حسابات في الأكتف دايركتوري من قبل النظام ده. فلما
        //  نرفع بياناتهم من إكسل، إحنا مش عايزين ننشئ حسابات جديدة — عايزين
        //  **نربط** كل طالب بحسابه القائم، عشان إجراءات زي «تخرّج» أو «فصل» تقدر
        //  تعطّل الحساب الصح.
        //
        //  الربط بيتم بصيغة h + الرقم الجامعي، وde المعيار المتفق عليه في الجامعة.
        //  العملية **قراءة فقط** من ناحية الأكتف دايركتوري: مفيش إنشاء ولا تعديل
        //  ولا تعطيل — بنسجّل بس اسم الحساب وحالته في قاعدة بيانات النظام.
        //
        //  قابلة لإعادة التشغيل أي عدد مرات؛ بتحدّث الحالة للمربوطين وبتربط الجداد.
        // ============================================================================
        //  مزامنة حسابات الشبكة مع الأكتف دايركتوري — قراءة من الدومين وكتابة عندنا بس.
        //  مفيش إنشاء ولا تعطيل ولا تعديل على أي حساب في الدومين هنا إطلاقًا.
        //
        //  وضعين منفصلين عن قصد:
        //   • RefreshLinked: الطلاب المربوطين بالفعل — بنقرا حالتهم الحقيقية ونحدّثها.
        //     ده اللي بيحل مشكلة «فعّلت الحساب في الدومين والنظام لسه شايفه معطّل».
        //     مالوش أي خطر لأنه مابيربطش حد جديد.
        //   • LinkNew: الطلاب اللي لسه مالهمش حساب مسجّل — بندوّر على h+الرقم الجامعي
        //     ونربط. ⚠️ ده اللي فيه الخطر: رقم جامعي غلط في الشيت = ربط الطالب بحساب
        //     شخص تاني، وأول «تخرّج» بعدها بيعطّل حساب الغلط. عشان كده فيه معاينة
        //     (dryRun) وفحص تشابه أسماء.
        // ============================================================================
        public async Task<AdLinkResultDto> SyncAdAccountsAsync(int actorId, AdSyncMode mode, bool dryRun = false)
        {
            var result = new AdLinkResultDto
            {
                Mode = mode == AdSyncMode.LinkNew ? "linkNew" : "refreshLinked",
                DryRun = dryRun
            };

            var query = _db.Students.Where(s => !s.IsDeleted && s.student_id != null);
            query = mode == AdSyncMode.LinkNew
                ? query.Where(s => s.ad_username == null || s.ad_username == "")
                : query.Where(s => s.ad_username != null && s.ad_username != "");

            var students = await query.OrderBy(s => s.student_id).ToListAsync();
            result.Scanned = students.Count;

            foreach (var student in students)
            {
                // في وضع التحديث بنستخدم اسم الحساب المسجّل فعلاً، مش المحسوب —
                // ممكن يكون اتربط يدويًا باسم مختلف عن h+الرقم.
                var sam = mode == AdSyncMode.RefreshLinked && !string.IsNullOrWhiteSpace(student.ad_username)
                    ? student.ad_username!
                    : "h" + student.student_id;

                var wasLinked = !string.IsNullOrWhiteSpace(student.ad_username);

                ADReadUserResult lookup;
                try
                {
                    lookup = await _adService.GetUserBySamAccountNameAsync(sam);
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    _logger.LogError(ex, "AD lookup threw for {Sam}", sam);
                    continue;
                }

                if (!lookup.Success)
                {
                    // «مش موجود» مختلف عن «الاستعلام فشل» — الأول بيانات، والتاني عطل.
                    // بنفرّق بينهم عشان المستخدم يعرف يعمل إيه.
                    if (lookup.Error != null && lookup.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    {
                        result.NotFound++;
                        if (result.NotFoundStudents.Count < 200)
                        {
                            result.NotFoundStudents.Add(new AdLinkStudentDto
                            {
                                StudentId = student.student_id,
                                FullName = student.full_name,
                                ExpectedAccount = sam
                            });
                        }
                    }
                    else
                    {
                        result.Failed++;
                        result.Error ??= lookup.Error;
                        _logger.LogWarning("AD lookup failed for {Sam}: {Error}", sam, lookup.Error);
                    }
                    continue;
                }

                var newStatus = lookup.AccountEnabled ? AdStatus.enabled : AdStatus.disabled;
                var statusChanged = student.ad_status != newStatus;

                if (statusChanged)
                {
                    result.StatusChanged++;
                    if (result.StatusChanges.Count < 200)
                    {
                        result.StatusChanges.Add(new AdStatusChangeDto
                        {
                            StudentId = student.student_id,
                            FullName = student.full_name,
                            Account = lookup.SamAccountName,
                            From = student.ad_status?.ToString() ?? "unknown",
                            To = newStatus.ToString()
                        });
                    }
                }

                // فحص تشابه الاسم — في وضع الربط بس، لأن المربوط بالفعل اتراجع قبل كده
                if (mode == AdSyncMode.LinkNew && !NamesLookRelated(student.full_name_english, student.full_name, lookup.DisplayName)
                    && result.NameMismatches.Count < 200)
                {
                    result.NameMismatches.Add(new AdNameMismatchDto
                    {
                        StudentId = student.student_id,
                        SystemName = student.full_name_english ?? student.full_name,
                        DirectoryName = lookup.DisplayName,
                        Account = lookup.SamAccountName
                    });
                }

                if (!dryRun)
                {
                    // ⚠️ التحديث كان يكتب الحالة الجديدة بلا أي قيد في السجل، فتفعيل
                    //    أو تعطيل تمّ في الدليل مباشرة كان يختفي أثره: لوحة النتيجة
                    //    تعرضه ثم تُغلق. والأسوأ أنه كان يمنع المسار الآخر من تسجيله —
                    //    فحص «تفاصيل» يقارن المسجَّل بالفعلي، وبعد أن يكون التحديث
                    //    ساواهما لا يجد فرقًا. فبدل أن يوثّق التغيير كان يطمسه.
                    if (wasLinked)
                    {
                        ReconcileStatus(student, lookup.AccountEnabled, actorId, "تحديث الحالات من الدليل");
                    }
                    else
                    {
                        // ربط أول مرة: ليس تغييرًا خارجيًا بل بداية المتابعة
                        student.ad_status = newStatus;
                        student.ad_last_sync_at = DateTime.UtcNow;
                        LogLifecycleEvent(student.Id,
                            newStatus == AdStatus.enabled ? "enabled" : "disabled", actorId,
                            $"تم ربط حساب الدليل {lookup.SamAccountName} بالطالب - حالته في الدليل: {newStatus}");
                    }

                    student.ad_username = lookup.SamAccountName;
                }

                if (wasLinked) result.AlreadyLinked++; else result.Linked++;
            }

            // ⚠️ الشرط كان (Linked > 0 || AlreadyLinked > 0) فقط. القيود المضافة أعلاه
            //    تُحفظ ضمن نفس SaveChanges، فلو مرّ التحديث بلا أي حساب مربوط لم
            //    يُحفظ شيء أصلًا. الحفظ الآن كلما وُجد ما يُحفظ.
            if (!dryRun && (result.Linked > 0 || result.AlreadyLinked > 0 || result.StatusChanged > 0))
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    user_id = actorId,
                    action = mode == AdSyncMode.LinkNew ? "ad_accounts_linked" : "ad_status_refreshed",
                    target_table = "Students",
                    target_id = 0,
                    action_at = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation(
                "AD sync ({Mode}{Dry}): scanned={Scanned} linked={Linked} already={Already} statusChanged={Changed} notFound={NotFound} failed={Failed}",
                result.Mode, dryRun ? ", dry-run" : "", result.Scanned, result.Linked, result.AlreadyLinked,
                result.StatusChanged, result.NotFound, result.Failed);

            return result;
        }

        // فحص متساهل عن قصد: كلمة واحدة مشتركة تكفي. الأسماء في الدومين مكتوبة
        // بترتيب وصيغ مختلفة، ففحص صارم كان هيحط كل الطلاب في قايمة التحذير
        // ويخلّيها بلا فايدة. الهدف نمسك الغلط الواضح: "Ahmed Ali" مقابل "Sara Hassan".
        private static bool NamesLookRelated(string? systemNameEn, string? systemNameAr, string? directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName)) return true;   // مفيش اسم نقارن بيه — مانحذّرش

            var dirTokens = Tokenize(directoryName);
            if (dirTokens.Count == 0) return true;

            foreach (var name in new[] { systemNameEn, systemNameAr })
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (Tokenize(name).Any(tok => dirTokens.Contains(tok)))
                    return true;
            }
            return false;
        }

        private static HashSet<string> Tokenize(string value) =>
            value.Split(new[] { ' ', '.', '-', '_', ',' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(p => p.Trim().ToLowerInvariant())
                 .Where(p => p.Length >= 3)
                 .ToHashSet();

        // الـ Base DN بيتبني من الدومين المضبوط في الإعدادات: nuh.edu.sa → DC=nuh,DC=edu,DC=sa
        // ⚠️ كانت المسارات الاحتياطية مكتوبة صراحةً بـ DC=globalgroups,DC=com — دومين
        //    قديم. لو صف الإعدادات في جدول ADConfigurations ناقص، النظام كان بيحاول
        //    ينشئ الحساب في دومين مش موجود من غير ما يقول إنه بيستخدم قيمة احتياطية.
        private string BaseDn() =>
            string.Join(",", (_adConfig.Domain ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries)
                                                    .Select(p => $"DC={p}"));

        private async Task<string> GetOuForStudentAsync(Student student)
        {
            var config = await _db.ADConfigurations.FirstOrDefaultAsync(c => c.ConfigKey == ADConfigurationKeys.StudentOuPath);
            var baseOu = config?.ConfigValue;
            if (string.IsNullOrWhiteSpace(baseOu))
            {
                baseOu = $"OU=New,OU=Students,{BaseDn()}";
                _logger.LogWarning("ADConfigurations['{Key}'] غير مضبوط - استخدام المسار الافتراضي {Ou}",
                    ADConfigurationKeys.StudentOuPath, baseOu);
            }

            var isMale = student.gender == Gender.Male;
            var genderOu = isMale ? "Male" : "Female";

            // ⚠️ المقارنة لازم تتجاهل حالة الحروف: المسار في الأكتف دايركتوري متكتب
            //    OU=NEW بحروف كبيرة، والفحص القديم كان حرفيًا — فكان بينتج مسار
            //    فيه OU=New مكررة (OU=Male,OU=New,OU=NEW,OU=STUDENTS,...) وde مسار
            //    مش موجود. أسماء الـ DN في LDAP مش حساسة لحالة الحروف أصلاً.
            if (baseOu.Contains("OU=New", StringComparison.OrdinalIgnoreCase))
            {
                return $"OU={genderOu},{baseOu}";
            }

            return $"OU={genderOu},OU=New,{baseOu}";
        }

        private async Task<string> GetGroupForStudentAsync(Student student)
        {
            var configMale = await _db.ADConfigurations.FirstOrDefaultAsync(c => c.ConfigKey == "male_group_dn");
            var configFemale = await _db.ADConfigurations.FirstOrDefaultAsync(c => c.ConfigKey == "female_group_dn");

            var isMale = student.gender == Gender.Male;
            var configured = isMale ? configMale?.ConfigValue : configFemale?.ConfigValue;
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            // نفس الملاحظة اللي فوق: الاحتياطي بيتبني من الدومين المضبوط مش من دومين مكتوب في الكود
            var fallback = isMale
                ? $"CN=NUH-Student-B,OU=Groups,{BaseDn()}"
                : $"CN=NUH-Student-G,OU=Groups,{BaseDn()}";
            _logger.LogWarning("مجموعة الطلاب ({Gender}) غير مضبوطة في ADConfigurations - استخدام {Group}",
                isMale ? "male" : "female", fallback);
            return fallback;
        }

        // ====================================================================
        //  مصالحة حالة الحساب مع الدليل — القاعدة الوحيدة في النظام.
        //
        //  ⚠️ كانت مكتوبة مرتين: مرة عند فتح «تفاصيل» الحساب في HousingAccountService،
        //     ومرة هنا في التحديث الجماعي. المقارنة نفسها والتحديث نفسه والصياغة
        //     نفسها تقريبًا — «تقريبًا» هي المشكلة. أي تعديل لاحق (كلمة في النص،
        //     حقل يُضاف، شرط يتغيّر) كان سيقع في نسخة ويُنسى في الأخرى، فيصير
        //     السجل نفسه بصيغتين حسب أي شاشة اكتشفت التغيير.
        //
        //  القاعدة هنا مرة واحدة، والمتغيّر الوحيد بين المسارين هو «أين اكتُشف».
        //  الدالة تُعدّل الكيان وتضيف القيد؛ الحفظ على المستدعي (كلاهما يعمل على
        //  نفس AppDbContext، فأي SaveChanges من أيّهما يحفظ ما أضافته).
        // ====================================================================
        public bool ReconcileStatus(Student student, bool actualEnabled, int actorId, string discoveredAt)
        {
            var actual = actualEnabled ? AdStatus.enabled : AdStatus.disabled;
            if (student.ad_status == actual)
            {
                student.ad_last_sync_at = DateTime.UtcNow;
                return false;
            }

            var previous = student.ad_status?.ToString() ?? "غير معروفة";
            student.ad_status = actual;
            student.ad_last_sync_at = DateTime.UtcNow;

            // الدليل لا يخبرنا *من* نفّذ الإجراء (اسم المنفّذ في سجل أحداث وحدة
            // التحكم بالنطاق)، فنسجّل ما نعرفه: أن التغيير جاء من خارج النظام،
            // ومن أين اكتشفناه، ومتى.
            LogLifecycleEvent(student.Id,
                actual == AdStatus.enabled ? "enabled" : "disabled",
                actorId,
                $"تغيّرت حالة الحساب من خارج النظام (من {previous} إلى {actual}) - اكتُشف عند {discoveredAt}");

            return true;
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
