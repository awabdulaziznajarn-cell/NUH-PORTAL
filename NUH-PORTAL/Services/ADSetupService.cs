using MapsterMapper;
using NUH_PORTAL.Core;
using NUH_PORTAL.Core.Exceptions;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.DTOs.ADSetup;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Services.Interfaces;

namespace NUH_PORTAL.Services
{
    // أدوات تشخيص AD - اتنقلت من ADSetupController
    public class ADSetupService : AppServiceBase, IADSetupService
    {
        private readonly IRepository<Student> _students;
        private readonly ActiveDirectoryService _adService;
        private readonly ILogger<ADSetupService> _logger;
        // ⚠️ أماكن الحسابات في الدليل - التعريف الوحيد في
        //    Services/AdDirectoryLayout.cs. كانت مكتوبة في الملف ده بالإيد
        //    على دومين بيئة قديمة بينما الدومين الحقيقي في الإعدادات،
        //    فأدوات التشخيص كانت بتفحص دليلًا تانيًا وترجع «غير موجود».
        private readonly AdDirectoryLayout _layout;

        public ADSetupService(
            IRepository<Student> students,
            ActiveDirectoryService adService,
            ILogger<ADSetupService> logger,
            AdDirectoryLayout layout,
            IUnitOfWork unitOfWork,
            IMapper mapper) : base(unitOfWork, mapper)
        {
            _students = students;
            _adService = adService;
            _logger = logger;
            _layout = layout;
        }

        public async Task<ADReadinessReport> GetReadinessAsync()
        {
            var result = new ADReadinessReport { Timestamp = DateTime.UtcNow };

            result.ADConnectivity = await _adService.CheckHealthAsync();

            if (result.ADConnectivity.IsReachable)
            {
                result.ServiceAccount = await _adService.ValidateServiceAccountBindAsync();

                if (result.ServiceAccount?.BindSuccessful == true)
                {
                    // المسارات اللي بينشئ فيها النظام فعلًا - نفس المصدر بالحرف،
                    // فالفحص بيقول لك حالة دليلك أنت لا دليل تاني.
                    result.OUs = new[]
                    {
                        await ValidateObjectAsync(await _layout.StudentOuAsync(Gender.Male), "organizationalUnit"),
                        await ValidateObjectAsync(await _layout.StudentOuAsync(Gender.Female), "organizationalUnit")
                    };

                    result.Groups = new[]
                    {
                        await ValidateObjectAsync(await _layout.StudentGroupAsync(Gender.Male), "group"),
                        await ValidateObjectAsync(await _layout.StudentGroupAsync(Gender.Female), "group")
                    };

                    result.PasswordPolicy = await _adService.GetPasswordPolicyAsync();
                }
            }

            return result;
        }

        public async Task<ADDryRunResult> GetDryRunAsync(string studentId)
        {
            var student = await _students.FindAsync(s => s.student_id == studentId && !s.IsDeleted)
                ?? throw UserFriendlyException.NotFound("Student not found");

            if (student.gender == null)
                throw new UserFriendlyException("Student has no gender set. Cannot determine target OU.", 400);

            var sAMAccountName = AdDirectoryLayout.SamAccountNameFor(student.student_id);

            var nameParts = (student.full_name_english ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var givenName = nameParts.Length > 0 ? nameParts[0] : sAMAccountName;
            var sn = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : givenName;

            var targetOu = await _layout.StudentOuAsync(student.gender);
            var targetGroup = await _layout.StudentGroupAsync(student.gender);

            // ⚠️ نفس مولّد الإنشاء الحقيقي: الجريان الجاف المفروض يوصف اللي
            //    هيحصل فعلًا. كان بيعرض "NUH@{الرقم}" وهو شكل مابيتكتبش أصلًا،
            //    ففحص سياسة كلمات المرور تحته كان بيفحص حاجة تانية.
            var tempPassword = AdDirectoryLayout.NewTempPassword();

            return new ADDryRunResult
            {
                Student = new
                {
                    student.student_id,
                    student.full_name,
                    student.full_name_english,
                    student.gender,
                    student.college,
                    student.department,
                    student.national_id,
                    student.phone,
                    student.housing_building,
                    student.academic_level
                },
                ProposedAccount = new
                {
                    sAMAccountName,
                    userPrincipalName = _layout.UpnFor(sAMAccountName),
                    displayName = student.full_name_english ?? sAMAccountName,
                    givenName = givenName,
                    sn = sn,
                    ou = targetOu,
                    group = targetGroup,
                    extensionAttribute1 = student.student_id,
                    extensionAttribute2 = student.national_id,
                    department = student.college ?? student.department ?? "",
                    title = "Student",
                    physicalDeliveryOfficeName = string.IsNullOrEmpty(student.housing_building)
                        ? ""
                        : $"Building {student.housing_building}",
                    description = $"Housing Student - {student.college ?? "N/A"} - Level {student.academic_level ?? "N/A"}"
                },
                TempPassword = new
                {
                    value = tempPassword,
                    length = tempPassword.Length,
                    requiresChange = true
                },
                LdapPath = $"CN={sAMAccountName},{targetOu}",
                PasswordPolicyCheck = await _adService.ValidatePasswordCompatibilityAsync(tempPassword)
            };
        }

        // ====================================================================
        //  ⚠️ الأداة دي بتعمل حساب **حقيقي ومفعّل** في الدليل النشط، فمدخلاتها
        //     بتتعامل معاملة مدخلات أي مسار إنشاء لا معاملة أداة تشخيص.
        //
        //     اللي اتغيّر:
        //       • مسار المجلد والمجموعة بقوا من الإعدادات لا من العميل. كان
        //         العميل بيبعت DN وبيتحطّ في CN={الرقم},{DN} وبيتضاف للمجموعة
        //         اللي بعتها - يعني حساب مفعّل في «Domain Admins» بطلب واحد.
        //       • الرقم الجامعي بيتفحص بنفس قاعدة النظام (Core/IdentityRules).
        //         من غير الفحص ده الرقم بيدخل في بناء الـ DN بلا هروب، فيفسد
        //         اسم الكائن أو يفشل الإنشاء.
        //       • كلمة المرور عشوائية. كانت "NUH@{الرقم الجامعي}" - والرقم
        //         مطبوع على كل ورقة في النظام.
        // ====================================================================
        public async Task<ADTestUserReport> RunTestUserAsync(ADTestUserRequest request)
        {
            if (!IdentityRules.IsValidStudentId(request.StudentId))
                throw new UserFriendlyException(IdentityRules.StudentIdError, 400);

            // القسم بيتحوّل لمسار من الإعدادات - ومفيش أي نصّ من العميل بيوصل للدليل.
            var gender = request.Gender ?? Gender.Male;

            var report = new ADTestUserReport
            {
                RequestedSamAccountName = AdDirectoryLayout.SamAccountNameFor(request.StudentId),
                TargetOu = await _layout.StudentOuAsync(gender),
                TargetGroup = await _layout.StudentGroupAsync(gender),
                Timestamp = DateTime.UtcNow
            };

            var sAMAccountName = report.RequestedSamAccountName;
            var password = AdDirectoryLayout.NewTempPassword();
            var targetOu = report.TargetOu;
            var targetGroup = report.TargetGroup;
            var userDn = $"CN={sAMAccountName},{targetOu}";

            report.Steps.Add(new ADTestStep
            {
                Step = "PreCheck",
                Description = $"Check if user '{sAMAccountName}' already exists",
                Status = "Running"
            });

            var existingUser = await _adService.GetUserBySamAccountNameAsync(sAMAccountName);
            if (existingUser.Success)
            {
                report.OverallSuccess = false;
                report.Steps[0].Status = "Skipped";
                report.Steps[0].Details = $"User '{sAMAccountName}' already exists in AD. Delete it first or use a different test ID.";
                report.Summary = "ABORTED: User already exists";
                return report;
            }
            report.Steps[0].Status = "Pass";
            report.Steps[0].Details = $"User '{sAMAccountName}' does not exist - ready to create";

            report.Steps.Add(new ADTestStep
            {
                Step = "CreateUser",
                Description = $"Create user '{sAMAccountName}' in OU '{targetOu}'",
                Status = "Running"
            });

            var createRequest = new ADCreateUserRequest
            {
                SamAccountName = sAMAccountName,
                UserPrincipalName = _layout.UpnFor(sAMAccountName),
                DisplayName = sAMAccountName,
                GivenName = "Test",
                Surname = "Account",
                TargetOu = targetOu,
                Description = "AD Pre-Production Test Account - Created " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                UserAccountControl = 544
            };

            var createResult = await _adService.CreateADUserAsync(createRequest);
            if (!createResult.Success)
                return Fail(report, createResult.Error, createResult.ErrorDetails, "FAILED at CreateUser");

            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = $"Created at DN: {createResult.DistinguishedName}";

            report.Steps.Add(new ADTestStep
            {
                Step = "SetPassword",
                Description = "Set password for the test account",
                Status = "Running"
            });

            var passwordResult = await _adService.SetUserPasswordAsync(userDn, password);
            if (!passwordResult.Success)
            {
                var r = Fail(report, passwordResult.Error, passwordResult.ErrorDetails, "FAILED at SetPassword - cleaning up created user");
                await TryCleanupUserAsync(userDn, sAMAccountName);
                return r;
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = $"Password set successfully (length: {password.Length})";

            report.Steps.Add(new ADTestStep
            {
                Step = "EnableAccount",
                Description = "Remove ACCOUNTDISABLE flag from userAccountControl",
                Status = "Running"
            });

            var enableResult = await _adService.ModifyUserAccountControlAsync(userDn, 66048);
            if (!enableResult.Success)
            {
                var r = Fail(report, enableResult.Error, enableResult.ErrorDetails, "FAILED at EnableAccount - cleaning up created user");
                await TryCleanupUserAsync(userDn, sAMAccountName);
                return r;
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = "Account enabled (userAccountControl = 66048)";

            report.Steps.Add(new ADTestStep
            {
                Step = "AddToGroup",
                Description = $"Add user to group '{targetGroup}'",
                Status = "Running"
            });

            var groupResult = await _adService.AddUserToGroupAsync(userDn, targetGroup);
            if (!groupResult.Success)
            {
                var r = Fail(report, groupResult.Error, groupResult.ErrorDetails, "FAILED at AddToGroup - cleaning up created user");
                await TryCleanupUserAsync(userDn, sAMAccountName);
                return r;
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = $"Added as member of '{targetGroup}'";

            report.Steps.Add(new ADTestStep
            {
                Step = "ReadUser",
                Description = "Read user attributes from AD for verification",
                Status = "Running"
            });

            var readResult = await _adService.GetUserBySamAccountNameAsync(sAMAccountName);
            if (!readResult.Success)
            {
                var r = Fail(report, readResult.Error, null, "FAILED at ReadUser - cleaning up created user");
                await TryCleanupUserAsync(userDn, sAMAccountName);
                return r;
            }
            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = "User attributes read successfully";
            report.ReadUserResult = readResult;

            report.Steps.Add(new ADTestStep
            {
                Step = "DeleteUser",
                Description = "Delete the test user from AD",
                Status = "Running"
            });

            var deleteResult = await _adService.DeleteADUserAsync(userDn);
            if (!deleteResult.Success)
                return Fail(report, deleteResult.Error, deleteResult.ErrorDetails, "FAILED at DeleteUser - manual cleanup required");

            report.Steps.Last().Status = "Pass";
            report.Steps.Last().Details = "User deleted from AD";

            report.OverallSuccess = true;
            report.Summary = "ALL TESTS PASSED - AD account creation pipeline verified end-to-end";
            return report;
        }

        // ----------------------------- Helpers -----------------------------

        private static ADTestUserReport Fail(ADTestUserReport report, string? error, string? details, string summary)
        {
            report.OverallSuccess = false;
            report.Steps.Last().Status = "Fail";
            report.Steps.Last().Error = error;
            report.Steps.Last().Details = details;
            report.Summary = summary;
            return report;
        }

        private async Task TryCleanupUserAsync(string userDn, string samAccountName)
        {
            try
            {
                var deleteResult = await _adService.DeleteADUserAsync(userDn);
                if (deleteResult.Success)
                    _logger.LogWarning("Cleanup: deleted partially-created user {SamAccountName}", samAccountName);
                else
                    _logger.LogWarning("Cleanup FAILED to delete user {SamAccountName}: {Error}", samAccountName, deleteResult.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cleanup threw exception for user {SamAccountName}", samAccountName);
            }
        }

        private async Task<ADObjectValidation> ValidateObjectAsync(string dn, string objectClass)
        {
            var result = await _adService.SearchObjectByDnAsync(dn, objectClass);
            return new ADObjectValidation
            {
                Dn = dn,
                Exists = result.exists,
                Error = result.error
            };
        }
    }
}
